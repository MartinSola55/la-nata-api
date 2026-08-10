using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.Internal;
using Template_Api.Core.Services;
using Template_Api.MercadoPago.Models;
using Template_Api.MercadoPago.Models.Constants;
using Template_Api.MercadoPago.Models.DTO;
using Template_Api.MercadoPago.Models.Internal;

namespace Template_Api.MercadoPago.Services
{
    public class MercadoPagoService(HttpClient httpClient, IOptions<MercadoPagoOptions> opts, APIContext context, TokenService tokenService, AuthService authService, IHostEnvironment env)
    {
        private const int ProviderResponseMaxLength = 8000;

        private readonly HttpClient _httpClient = httpClient;
        private readonly MercadoPagoOptions _opts = opts.Value;
        private readonly APIContext _db = context;
        private readonly Token _token = tokenService.GetToken();
        private readonly AuthService _authService = authService;
        private readonly IHostEnvironment _env = env;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #region Methods
        public async Task<BaseResponse<GetPaymentResponse>> GetPayment(GetPaymentRequest rq)
        {
            var rs = new BaseResponse<GetPaymentResponse>();

            if (rq == null || (!rq.Id.HasValue && string.IsNullOrWhiteSpace(rq.ExternalReference) && string.IsNullOrWhiteSpace(rq.PaymentId)))
                return rs.SetError(Messages.Error.FieldsRequired(["Id", "ExternalReference", "PaymentId"]));

            var query = _db.MercadoPagoPayment.AsNoTracking().AsQueryable();

            if (!_authService.IsAdmin())
                query = query.Where(x => x.UserId == _token.UserId);

            if (rq.Id.HasValue)
                query = query.Where(x => x.Id == rq.Id.Value);
            else if (!string.IsNullOrWhiteSpace(rq.ExternalReference))
            {
                var externalReference = rq.ExternalReference.Trim();
                query = query.Where(x => x.ExternalReference == externalReference);
            }
            else
            {
                var paymentId = rq.PaymentId.Trim();
                query = query.Where(x => x.PaymentId == paymentId);
            }

            var payment = await query.FirstOrDefaultAsync();
            if (payment == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pago"));

            rs.Data = MapPayment(payment);
            return rs;
        }

        public async Task<BaseResponse<CreatePreferenceResponse>> CreatePreference(CreatePreferenceRequest rq)
        {
            var rs = new BaseResponse<CreatePreferenceResponse>();

            var configError = ValidateConfiguration(requireWebhookSecret: false);
            if (!string.IsNullOrEmpty(configError))
                return rs.SetError(configError);

            var payerEmail = NormalizeEmail(rq?.PayerEmail) ?? NormalizeEmail(_token.Email);

            var validationError = ValidateCreatePreference(rq, payerEmail);
            if (!string.IsNullOrEmpty(validationError))
                return rs.SetError(validationError);

            var externalReference = rq.ExternalReference.Trim();
            var payment = await _db.MercadoPagoPayment.FirstOrDefaultAsync(x => x.ExternalReference == externalReference);

            if (payment != null)
            {
                if (!_authService.IsAdmin() && payment.UserId != _token.UserId)
                    return rs.SetError(Messages.Error.Unauthorized());

                if (payment.Status == MercadoPagoPaymentStatus.Approved)
                    return rs.SetError(Messages.Error.InvalidField("referencia externa"));

                if (CanReusePreference(payment))
                {
                    rs.Data = MapCreatePreference(payment);
                    return rs;
                }
            }

            var total = CalculateTotal(rq);
            var providerResponse = await CreatePreferenceInProvider(rq, externalReference, payerEmail, _token.Name, _token.LastName);
            if (!providerResponse.Success)
                return rs.SetError(Messages.Error.MercadoPagoException());

            payment ??= new MercadoPagoPayment
            {
                ExternalReference = externalReference
            };

            payment.UserId = _token.UserId;
            payment.PreferenceId = providerResponse.PreferenceId;
            payment.PayerEmail = payerEmail;
            payment.TransactionAmount = total;
            payment.CurrencyId = _opts.CurrencyId;
            payment.Status = MercadoPagoPaymentStatus.PreferenceCreated;
            payment.StatusDetail = null;
            payment.InitPoint = providerResponse.InitPoint;
            payment.SandboxInitPoint = providerResponse.SandboxInitPoint;
            payment.ProviderResponse = Truncate(providerResponse.ProviderResponse, ProviderResponseMaxLength);
            payment.Error = null;
            payment.UpdatedAt = DateTime.UtcNow;

            try
            {
                if (payment.Id == 0)
                    _db.MercadoPagoPayment.Add(payment);

                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.MercadoPago.PreferenceCreated();
            rs.Data = MapCreatePreference(payment);
            return rs;
        }

        public async Task<BaseResponse<CreatePreferenceResponse>> CreateCartPreference(CreateCartPreferenceRequest rq)
        {
            var rs = new BaseResponse<CreatePreferenceResponse>();

            var configError = ValidateConfiguration(requireWebhookSecret: false);
            if (!string.IsNullOrEmpty(configError))
                return rs.SetError(configError);

            var payerEmail = NormalizeEmail(rq?.PayerEmail) ?? NormalizeEmail(_token.Email);
            var validationError = ValidateCreateCartPreference(rq, payerEmail);
            if (!string.IsNullOrEmpty(validationError))
                return rs.SetError(validationError);

            var cart = await _db
                .Cart
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.CartId);

            if (cart == null)
                return rs.SetError(Messages.Error.EntityNotFound("Carrito"));

            if (!_authService.IsAdmin() && cart.UserId != _token.UserId)
                return rs.SetError(Messages.Error.Unauthorized());

            var cartValidationError = await ValidateCartForPayment(cart);
            if (!string.IsNullOrEmpty(cartValidationError))
                return rs.SetError(cartValidationError);

            var existingPayment = await _db.MercadoPagoPayment.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.PaidAt == null);
            if (existingPayment != null && CanReusePreference(existingPayment))
            {
                rs.Data = MapCreatePreference(existingPayment);
                return rs;
            }

            var externalReference = BuildCartExternalReference(cart.Id);
            var preferenceRequest = new CreatePreferenceRequest
            {
                ExternalReference = externalReference,
                PayerEmail = payerEmail,
                PayerName = rq.PayerName,
                PayerLastName = rq.PayerLastName,
                BinaryMode = rq.BinaryMode,
                Items = [.. cart.Products
                    .OrderBy(x => x.ProductName)
                    .Select(x => new CreatePreferenceRequest.PreferenceItem
                    {
                        Id = x.ProductId.ToString(),
                        Title = x.ProductName,
                        Quantity = x.Quantity,
                        UnitPrice = x.SettedPrice
                    })]
            };

            var providerResponse = await CreatePreferenceInProvider(preferenceRequest, externalReference, payerEmail, _token.Name, _token.LastName);
            if (!providerResponse.Success)
                return rs.SetError(Messages.Error.MercadoPagoException());

            var payment = new MercadoPagoPayment
            {
                UserId = _token.UserId,
                CartId = cart.Id,
                PreferenceId = providerResponse.PreferenceId,
                ExternalReference = externalReference,
                PayerEmail = payerEmail,
                TransactionAmount = cart.Total,
                CurrencyId = _opts.CurrencyId,
                Status = MercadoPagoPaymentStatus.PreferenceCreated,
                InitPoint = providerResponse.InitPoint,
                SandboxInitPoint = providerResponse.SandboxInitPoint,
                ProviderResponse = Truncate(providerResponse.ProviderResponse, ProviderResponseMaxLength)
            };

            try
            {
                _db.MercadoPagoPayment.Add(payment);
                cart.Status = CartStatus.PaymentPending;
                cart.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.MercadoPago.PreferenceCreated();
            rs.Data = MapCreatePreference(payment);
            return rs;
        }

        public async Task<BaseResponse<ProcessWebhookResponse>> ProcessWebhook(MercadoPagoWebhookRequest rq, HttpRequest request)
        {
            var rs = new BaseResponse<ProcessWebhookResponse>();

            var configError = ValidateConfiguration(requireWebhookSecret: true);
            if (!string.IsNullOrEmpty(configError))
                return rs.SetError(configError);

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var signatureError = ValidateWebhookSignature(request);
            if (!string.IsNullOrEmpty(signatureError))
                return rs.SetError(signatureError);

            if (!string.Equals(rq.Type, "payment", StringComparison.OrdinalIgnoreCase))
            {
                rs.Message = Messages.MercadoPago.NotificationIgnored();
                return rs;
            }

            var paymentId = GetPaymentIdFromWebhook(rq, request);
            if (string.IsNullOrWhiteSpace(paymentId))
                return rs.SetError(Messages.Error.InvalidField("pago"));

            var providerPayment = await GetPaymentFromProvider(paymentId);
            if (!providerPayment.Success)
                return rs.SetError(Messages.Error.MercadoPagoException());

            var payment = await FindPayment(providerPayment.PaymentId, providerPayment.ExternalReference);
            if (payment == null)
            {
                payment = new MercadoPagoPayment
                {
                    ExternalReference = providerPayment.ExternalReference,
                    CreatedAt = DateTime.UtcNow
                };
                _db.MercadoPagoPayment.Add(payment);
            }

            var providerValidationError = ValidateProviderPayment(payment, providerPayment);
            if (!string.IsNullOrEmpty(providerValidationError))
            {
                payment.Error = providerValidationError;
                payment.LastNotificationAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception)
                {
                    return rs.SetError(Messages.Error.Exception());
                }

                return rs.SetError(providerValidationError);
            }

            var wasAlreadyPaid = payment.PaidAt.HasValue;

            payment.PaymentId = providerPayment.PaymentId;
            payment.ExternalReference = providerPayment.ExternalReference ?? payment.ExternalReference;
            payment.PayerEmail = providerPayment.PayerEmail ?? payment.PayerEmail;
            payment.TransactionAmount = providerPayment.TransactionAmount ?? payment.TransactionAmount;
            payment.CurrencyId = providerPayment.CurrencyId ?? payment.CurrencyId;
            payment.Status = providerPayment.Status ?? MercadoPagoPaymentStatus.Unknown;
            payment.StatusDetail = providerPayment.StatusDetail;
            payment.ProviderResponse = Truncate(providerPayment.ProviderResponse, ProviderResponseMaxLength);
            payment.Error = null;
            payment.LastNotificationAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                if (payment.Status == MercadoPagoPaymentStatus.Approved && !wasAlreadyPaid)
                {
                    payment.PaidAt = providerPayment.DateApproved ?? DateTime.UtcNow;

                    var stockError = await ApplyCartPayment(payment);
                    if (!string.IsNullOrEmpty(stockError))
                    {
                        payment.PaidAt = null;
                        payment.Error = stockError;
                        await _db.SaveChangesAsync();
                        await tx.CommitAsync();
                        return rs.SetError(stockError);
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.MercadoPago.NotificationProcessed();
            rs.Data = new ProcessWebhookResponse
            {
                Id = payment.Id,
                PaymentId = payment.PaymentId,
                ExternalReference = payment.ExternalReference,
                Status = payment.Status,
                StatusDetail = payment.StatusDetail,
                ProcessedAt = DateTime.UtcNow
            };

            return rs;
        }
        #endregion

        #region Validations
        private string ValidateConfiguration(bool requireWebhookSecret)
        {
            if (!_opts.Enabled || string.IsNullOrWhiteSpace(_opts.AccessToken))
                return Messages.Error.MercadoPagoNotConfigured();

            if (string.IsNullOrWhiteSpace(_opts.CurrencyId))
                return Messages.Error.FieldRequired("moneda");

            var urls = new Dictionary<string, string>
            {
                { "URL de éxito", _opts.SuccessUrl },
                { "URL de pendiente", _opts.PendingUrl },
                { "URL de fallo", _opts.FailureUrl },
                { "URL de notificación", _opts.NotificationUrl }
            };

            foreach (var url in urls)
            {
                var error = ValidateUrl(url.Value, url.Key);
                if (!string.IsNullOrEmpty(error))
                    return error;
            }

            if (requireWebhookSecret && string.IsNullOrWhiteSpace(_opts.WebhookSecret))
                return Messages.Error.MercadoPagoNotConfigured();

            return null;
        }

        private static string ValidateCreatePreference(CreatePreferenceRequest rq, string payerEmail)
        {
            if (rq == null)
                return Messages.Error.FieldsRequired();

            if (string.IsNullOrWhiteSpace(rq.ExternalReference))
                return Messages.Error.FieldRequired("referencia externa");

            if (string.IsNullOrWhiteSpace(payerEmail))
                return Messages.Error.FieldRequired("email");

            if (!new EmailAddressAttribute().IsValid(payerEmail))
                return Messages.Error.InvalidEmail();

            if (rq.Items == null || rq.Items.Count == 0)
                return Messages.Error.FieldRequired("items");

            for (var i = 0; i < rq.Items.Count; i++)
            {
                var item = rq.Items[i];

                if (item == null)
                    return Messages.Error.InvalidField("item");

                if (string.IsNullOrWhiteSpace(item.Title))
                    return Messages.Error.FieldRequired("título");

                if (item.Quantity <= 0)
                    return Messages.Error.FieldGraterThanZero("cantidad");

                if (item.UnitPrice <= 0)
                    return Messages.Error.FieldGraterThanZero("precio");
            }

            if (CalculateTotal(rq) <= 0)
                return Messages.Error.FieldGraterThanZero("total");

            return null;
        }

        private static string ValidateCreateCartPreference(CreateCartPreferenceRequest rq, string payerEmail)
        {
            if (rq == null)
                return Messages.Error.FieldsRequired();

            if (rq.CartId <= 0)
                return Messages.Error.FieldGraterThanZero("carrito");

            if (string.IsNullOrWhiteSpace(payerEmail))
                return Messages.Error.FieldRequired("email");

            if (!new EmailAddressAttribute().IsValid(payerEmail))
                return Messages.Error.InvalidEmail();

            return null;
        }

        private async Task<string> ValidateCartForPayment(Cart cart)
        {
            if (cart.Status == CartStatus.Paid || cart.Status == CartStatus.Cancelled)
                return Messages.Error.InvalidField("carrito");

            if (cart.Products == null || cart.Products.Count == 0)
                return Messages.Error.FieldRequired("productos");

            if (cart.Total <= 0)
                return Messages.Error.FieldGraterThanZero("total");

            var productIds = cart.Products.Select(x => x.ProductId).ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var cartProduct in cart.Products)
            {
                var product = products.FirstOrDefault(x => x.Id == cartProduct.ProductId);
                if (product == null)
                    return Messages.Error.EntityNotFound("Producto");

                if (!product.IsActive)
                    return Messages.Error.InvalidField("producto");

                if (product.Stock < cartProduct.Quantity)
                    return Messages.Error.ProductWithoutStock();
            }

            var calculatedTotal = cart.Products.Sum(x => x.Quantity * x.SettedPrice);
            if (calculatedTotal != cart.Total)
                return Messages.Error.InvalidField("total");

            if (cart.Products.Any(x => x.SettedPrice <= 0))
                return Messages.Error.FieldGraterThanZero("precio");

            if (cart.Products.Any(x => x.Quantity <= 0))
                return Messages.Error.FieldGraterThanZero("cantidad");

            return null;
        }

        private async Task<string> ValidateCartStock(Cart cart)
        {
            var productIds = cart.Products.Select(x => x.ProductId).ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var cartProduct in cart.Products)
            {
                var product = products.FirstOrDefault(x => x.Id == cartProduct.ProductId);
                if (product == null)
                    return Messages.Error.EntityNotFound("Producto");

                if (product.Stock < cartProduct.Quantity)
                    return Messages.Error.ProductWithoutStock();
            }

            return null;
        }

        private async Task DiscountCartStock(Cart cart)
        {
            var productIds = cart.Products.Select(x => x.ProductId).ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var cartProduct in cart.Products)
            {
                var product = products.First(x => x.Id == cartProduct.ProductId);
                product.Stock -= cartProduct.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        private static string ValidateCartForProviderPayment(Cart cart)
        {
            if (cart == null)
                return Messages.Error.EntityNotFound("Carrito");

            if (cart.Status == CartStatus.Cancelled)
                return Messages.Error.InvalidField("carrito");

            if (cart.Products == null || cart.Products.Count == 0)
                return Messages.Error.FieldRequired("productos");

            if (cart.Products.Any(x => x.Quantity <= 0))
                return Messages.Error.FieldGraterThanZero("cantidad");

            if (cart.Products.Any(x => x.SettedPrice <= 0))
                return Messages.Error.FieldGraterThanZero("precio");

            if (cart.Total <= 0 || cart.Total != cart.Products.Sum(x => x.Quantity * x.SettedPrice))
                return Messages.Error.InvalidField("total");

            return null;
        }

        private async Task<string> ApplyCartPayment(MercadoPagoPayment payment)
        {
            if (!payment.CartId.HasValue)
                return null;

            var cart = await _db
                .Cart
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == payment.CartId.Value);

            var cartValidationError = ValidateCartForProviderPayment(cart);
            if (!string.IsNullOrEmpty(cartValidationError))
                return cartValidationError;

            if (cart.Status == CartStatus.Paid)
                return null;

            var stockError = await ValidateCartStock(cart);
            if (!string.IsNullOrEmpty(stockError))
                return stockError;

            await DiscountCartStock(cart);

            cart.Status = CartStatus.Paid;
            cart.PaidAt = payment.PaidAt ?? DateTime.UtcNow;
            cart.UpdatedAt = DateTime.UtcNow;

            return null;
        }

        private string ValidateUrl(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Messages.Error.FieldRequired(fieldName);

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return Messages.Error.InvalidField(fieldName);

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return Messages.Error.InvalidField(fieldName);

            if (!_env.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps)
                return Messages.Error.InvalidField(fieldName);

            return null;
        }

        private string ValidateWebhookSignature(HttpRequest request)
        {
            if (request == null)
                return Messages.Error.InvalidPaymentNotification();

            var signature = request.Headers["x-signature"].FirstOrDefault();
            var requestId = request.Headers["x-request-id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(requestId))
                return Messages.Error.InvalidPaymentNotification();

            var signatureParts = ParseSignature(signature);
            if (!signatureParts.TryGetValue("ts", out var ts) || !signatureParts.TryGetValue("v1", out var hash))
                return Messages.Error.InvalidPaymentNotification();

            if (_opts.WebhookToleranceSeconds > 0 && !ValidateTimestamp(ts))
                return Messages.Error.InvalidPaymentNotification();

            var template = BuildSignatureTemplate(request, ts);
            var expectedHash = CalculateHmac(template, _opts.WebhookSecret);

            if (!TryHexToBytes(hash, out var hashBytes) || !TryHexToBytes(expectedHash, out var expectedBytes))
                return Messages.Error.InvalidPaymentNotification();

            if (hashBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(hashBytes, expectedBytes))
                return Messages.Error.InvalidPaymentNotification();

            return null;
        }

        private string ValidateProviderPayment(MercadoPagoPayment payment, ProviderPayment providerPayment)
        {
            if (payment.TransactionAmount > 0 && providerPayment.TransactionAmount.HasValue && payment.TransactionAmount != providerPayment.TransactionAmount.Value)
                return Messages.Error.InvalidField("monto");

            if (!string.IsNullOrWhiteSpace(payment.CurrencyId) &&
                !string.IsNullOrWhiteSpace(providerPayment.CurrencyId) &&
                !string.Equals(payment.CurrencyId, providerPayment.CurrencyId, StringComparison.OrdinalIgnoreCase))
                return Messages.Error.InvalidField("moneda");

            return null;
        }
        #endregion

        #region Helpers
        private async Task<ProviderPreference> CreatePreferenceInProvider(CreatePreferenceRequest rq, string externalReference, string payerEmail, string tokenName, string tokenLastName)
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = rq.Items.Select(x => new
                {
                    id = string.IsNullOrWhiteSpace(x.Id) ? null : x.Id.Trim(),
                    title = x.Title.Trim(),
                    description = string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(),
                    picture_url = string.IsNullOrWhiteSpace(x.PictureUrl) ? null : x.PictureUrl.Trim(),
                    category_id = string.IsNullOrWhiteSpace(x.CategoryId) ? null : x.CategoryId.Trim(),
                    quantity = x.Quantity,
                    currency_id = _opts.CurrencyId,
                    unit_price = x.UnitPrice
                }),
                payer = new
                {
                    email = payerEmail,
                    name = string.IsNullOrWhiteSpace(rq.PayerName) ? tokenName : rq.PayerName.Trim(),
                    surname = string.IsNullOrWhiteSpace(rq.PayerLastName) ? tokenLastName : rq.PayerLastName.Trim()
                },
                back_urls = new
                {
                    success = _opts.SuccessUrl,
                    pending = _opts.PendingUrl,
                    failure = _opts.FailureUrl
                },
                notification_url = _opts.NotificationUrl,
                auto_return = string.IsNullOrWhiteSpace(_opts.AutoReturn) ? null : _opts.AutoReturn,
                external_reference = externalReference,
                binary_mode = rq.BinaryMode,
                statement_descriptor = string.IsNullOrWhiteSpace(_opts.StatementDescriptor) ? null : _opts.StatementDescriptor
            }, _jsonOptions);

            try
            {
                using var request = BuildProviderRequest(HttpMethod.Post, "/checkout/preferences", payload);
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ProviderPreference
                    {
                        Success = false,
                        ProviderResponse = body
                    };
                }

                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                return new ProviderPreference
                {
                    Success = true,
                    PreferenceId = GetJsonString(root, "id"),
                    InitPoint = GetJsonString(root, "init_point"),
                    SandboxInitPoint = GetJsonString(root, "sandbox_init_point"),
                    ProviderResponse = body
                };
            }
            catch (Exception ex)
            {
                return new ProviderPreference
                {
                    Success = false,
                    ProviderResponse = ex.Message
                };
            }
        }

        private async Task<ProviderPayment> GetPaymentFromProvider(string paymentId)
        {
            try
            {
                using var request = BuildProviderRequest(HttpMethod.Get, "/v1/payments/" + Uri.EscapeDataString(paymentId));
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ProviderPayment
                    {
                        Success = false,
                        ProviderResponse = body
                    };
                }

                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                return new ProviderPayment
                {
                    Success = true,
                    PaymentId = GetJsonString(root, "id"),
                    ExternalReference = GetJsonString(root, "external_reference"),
                    PayerEmail = GetNestedJsonString(root, "payer", "email"),
                    TransactionAmount = GetJsonDecimal(root, "transaction_amount"),
                    CurrencyId = GetJsonString(root, "currency_id"),
                    Status = GetJsonString(root, "status"),
                    StatusDetail = GetJsonString(root, "status_detail"),
                    DateApproved = GetJsonDateTime(root, "date_approved"),
                    ProviderResponse = body
                };
            }
            catch (Exception ex)
            {
                return new ProviderPayment
                {
                    Success = false,
                    ProviderResponse = ex.Message
                };
            }
        }

        private async Task<MercadoPagoPayment> FindPayment(string paymentId, string externalReference)
        {
            if (!string.IsNullOrWhiteSpace(paymentId) && !string.IsNullOrWhiteSpace(externalReference))
                return await _db.MercadoPagoPayment.FirstOrDefaultAsync(x => x.PaymentId == paymentId || x.ExternalReference == externalReference);

            if (!string.IsNullOrWhiteSpace(paymentId))
                return await _db.MercadoPagoPayment.FirstOrDefaultAsync(x => x.PaymentId == paymentId);

            if (!string.IsNullOrWhiteSpace(externalReference))
                return await _db.MercadoPagoPayment.FirstOrDefaultAsync(x => x.ExternalReference == externalReference);

            return null;
        }

        private HttpRequestMessage BuildProviderRequest(HttpMethod method, string path, string payload = null)
        {
            var request = new HttpRequestMessage(method, GetProviderUrl(path));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AccessToken);

            if (!string.IsNullOrWhiteSpace(payload))
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            return request;
        }

        private string GetProviderUrl(string path)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_opts.BaseUrl) ? "https://api.mercadopago.com" : _opts.BaseUrl.TrimEnd('/');
            return baseUrl + path;
        }

        private static string GetPaymentIdFromWebhook(MercadoPagoWebhookRequest rq, HttpRequest request)
        {
            var queryPaymentId = request.Query["data.id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(queryPaymentId))
                return queryPaymentId.Trim();

            return GetJsonElementString(rq.Data?.Id)?.Trim();
        }

        private static string BuildSignatureTemplate(HttpRequest request, string timestamp)
        {
            var parts = new List<string>();
            var dataId = request.Query["data.id"].FirstOrDefault();
            var requestId = request.Headers["x-request-id"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(dataId))
                parts.Add("id:" + dataId.Trim().ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(requestId))
                parts.Add("request-id:" + requestId.Trim());

            if (!string.IsNullOrWhiteSpace(timestamp))
                parts.Add("ts:" + timestamp.Trim());

            return string.Join(";", parts) + ";";
        }

        private static Dictionary<string, string> ParseSignature(string signature)
        {
            return signature
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2))
                .Where(x => x.Length == 2)
                .ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private bool ValidateTimestamp(string timestamp)
        {
            if (!long.TryParse(timestamp, out var rawTimestamp))
                return false;

            try
            {
                var sentAt = timestamp.Length <= 10
                    ? DateTimeOffset.FromUnixTimeSeconds(rawTimestamp)
                    : DateTimeOffset.FromUnixTimeMilliseconds(rawTimestamp);

                return Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalSeconds) <= _opts.WebhookToleranceSeconds;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static string CalculateHmac(string value, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool TryHexToBytes(string value, out byte[] bytes)
        {
            bytes = null;

            try
            {
                bytes = Convert.FromHexString(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool CanReusePreference(MercadoPagoPayment payment)
        {
            return !string.IsNullOrWhiteSpace(payment.PreferenceId) &&
                payment.Status != MercadoPagoPaymentStatus.Rejected &&
                payment.Status != MercadoPagoPaymentStatus.Cancelled &&
                payment.Status != MercadoPagoPaymentStatus.Refunded &&
                payment.Status != MercadoPagoPaymentStatus.ChargedBack;
        }

        private static decimal CalculateTotal(CreatePreferenceRequest rq)
        {
            return rq.Items.Sum(x => x.Quantity * x.UnitPrice);
        }

        private static string BuildCartExternalReference(int cartId)
        {
            return "CART-" + cartId + "-" + Guid.NewGuid().ToString("N");
        }

        private static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return email.Trim().ToLower();
        }

        private static CreatePreferenceResponse MapCreatePreference(MercadoPagoPayment payment)
        {
            return new CreatePreferenceResponse
            {
                Id = payment.Id,
                CartId = payment.CartId,
                PreferenceId = payment.PreferenceId,
                ExternalReference = payment.ExternalReference,
                Status = payment.Status,
                TransactionAmount = payment.TransactionAmount,
                CurrencyId = payment.CurrencyId,
                InitPoint = payment.InitPoint,
                SandboxInitPoint = payment.SandboxInitPoint
            };
        }

        private static GetPaymentResponse MapPayment(MercadoPagoPayment payment)
        {
            return new GetPaymentResponse
            {
                Id = payment.Id,
                UserId = payment.UserId,
                CartId = payment.CartId,
                PreferenceId = payment.PreferenceId,
                PaymentId = payment.PaymentId,
                ExternalReference = payment.ExternalReference,
                PayerEmail = payment.PayerEmail,
                TransactionAmount = payment.TransactionAmount,
                CurrencyId = payment.CurrencyId,
                Status = payment.Status,
                StatusDetail = payment.StatusDetail,
                InitPoint = payment.InitPoint,
                SandboxInitPoint = payment.SandboxInitPoint,
                PaidAt = payment.PaidAt,
                LastNotificationAt = payment.LastNotificationAt,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt
            };
        }

        private static string GetJsonString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return GetJsonElementString(property);
        }

        private static string GetNestedJsonString(JsonElement root, string propertyName, string nestedPropertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return GetJsonString(property, nestedPropertyName);
        }

        private static string GetJsonElementString(JsonElement? element)
        {
            if (!element.HasValue)
                return null;

            return element.Value.ValueKind switch
            {
                JsonValueKind.String => element.Value.GetString(),
                JsonValueKind.Number => element.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static decimal? GetJsonDecimal(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
                return value;

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return value;

            return null;
        }

        private static DateTime? GetJsonDateTime(JsonElement root, string propertyName)
        {
            var value = GetJsonString(root, propertyName);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
                return date.ToUniversalTime();

            return null;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }
        #endregion

        #region Private
        private class ProviderPreference
        {
            public bool Success { get; set; }
            public string PreferenceId { get; set; }
            public string InitPoint { get; set; }
            public string SandboxInitPoint { get; set; }
            public string ProviderResponse { get; set; }
        }

        private class ProviderPayment
        {
            public bool Success { get; set; }
            public string PaymentId { get; set; }
            public string ExternalReference { get; set; }
            public string PayerEmail { get; set; }
            public decimal? TransactionAmount { get; set; }
            public string CurrencyId { get; set; }
            public string Status { get; set; }
            public string StatusDetail { get; set; }
            public DateTime? DateApproved { get; set; }
            public string ProviderResponse { get; set; }
        }
        #endregion
    }
}
