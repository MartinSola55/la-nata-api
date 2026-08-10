using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.WhatsApp.Models;
using Template_Api.WhatsApp.Models.Constants;
using Template_Api.WhatsApp.Models.DTO;
using Template_Api.WhatsApp.Models.Internal;

namespace Template_Api.WhatsApp.Services
{
    public class WhatsAppService(HttpClient httpClient, IOptions<WhatsAppOptions> opts, APIContext context)
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly WhatsAppOptions _opts = opts.Value;
        private readonly APIContext _db = context;

        public async Task<BaseResponse<SendTextResponse>> SendText(SendTextRequest rq)
        {
            var rs = new BaseResponse<SendTextResponse>();

            if (string.IsNullOrWhiteSpace(rq.PhoneNumber) || string.IsNullOrWhiteSpace(rq.Message))
                return rs.SetError(Messages.Error.FieldsRequired(["PhoneNumber", "Message"]));

            if (!_opts.Enabled || string.IsNullOrWhiteSpace(_opts.BaseUrl) || string.IsNullOrWhiteSpace(_opts.ApiKey) || string.IsNullOrWhiteSpace(_opts.InstanceName))
                return rs.SetError(Messages.Error.WhatsAppNotConfigured());

            var phoneNumber = NormalizePhoneNumber(rq.PhoneNumber);
            if (phoneNumber.Length < 8)
                return rs.SetError(Messages.Error.InvalidField("teléfono"));

            var log = new WhatsAppMessage
            {
                PhoneNumber = phoneNumber,
                Message = rq.Message,
                Provider = "Evolution",
                Status = WhatsAppMessageStatus.Pending
            };

            HttpResponseMessage response;
            string body;

            try
            {
                using var request = BuildRequest(phoneNumber, rq.Message);
                response = await _httpClient.SendAsync(request);
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                log.Status = WhatsAppMessageStatus.Failed;
                log.Error = ex.Message;

                try
                {
                    await SaveLog(log);
                }
                catch (Exception)
                {
                    return rs.SetError(Messages.Error.Exception());
                }

                return rs.SetError(Messages.Error.WhatsAppException());
            }

            log.ProviderResponse = Truncate(body, 4000);
            log.Status = response.IsSuccessStatusCode ? WhatsAppMessageStatus.Sent : WhatsAppMessageStatus.Failed;
            log.SentAt = response.IsSuccessStatusCode ? DateTime.UtcNow : null;
            log.Error = response.IsSuccessStatusCode ? null : body;
            response.Dispose();

            try
            {
                await SaveLog(log);
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            if (log.Status != WhatsAppMessageStatus.Sent)
                return rs.SetError(Messages.Error.WhatsAppException());

            rs.Data = new SendTextResponse
            {
                Id = log.Id,
                PhoneNumber = log.PhoneNumber,
                Status = log.Status,
                SentAt = log.SentAt
            };
            return rs;
        }

        #region Helpers
        private async Task SaveLog(WhatsAppMessage log)
        {
            _db.WhatsAppMessage.Add(log);
            await _db.SaveChangesAsync();
        }

        private HttpRequestMessage BuildRequest(string phoneNumber, string message)
        {
            var baseUrl = _opts.BaseUrl.TrimEnd('/');
            var endpoint = $"{baseUrl}/message/sendText/{_opts.InstanceName}";
            var payload = JsonSerializer.Serialize(new
            {
                number = phoneNumber,
                text = message
            });

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("apikey", _opts.ApiKey);

            return request;
        }

        private static string NormalizePhoneNumber(string phoneNumber)
        {
            return new string(phoneNumber.Where(char.IsDigit).ToArray());
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }
        #endregion
    }
}
