using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.Cart;

namespace Template_Api.Core.Services
{
    public class CartService(APIContext context, TokenService tokenService, AuthService authService)
    {
        private readonly APIContext _db = context;
        private readonly TokenService _tokenService = tokenService;
        private readonly AuthService _authService = authService;

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            var rs = new BaseResponse<GetAllResponse>();
            var token = _tokenService.GetToken();

            rq ??= new GetAllRequest();

            if (rq.Page <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("página"));

            var query = _db
                .Cart
                .Include(x => x.User)
                .Include(x => x.Products)
                .AsQueryable();

            if (!_authService.IsAdmin())
                query = query.Where(x => x.UserId == token.UserId);
            else if (rq.UserId.HasValue)
                query = query.Where(x => x.UserId == rq.UserId.Value);

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            rs.Data = new GetAllResponse
            {
                Items = await query
                    .Select(x => new GetAllResponse.Item
                    {
                        Id = x.Id,
                        UserId = x.UserId,
                        UserEmail = x.User.Email,
                        Status = x.Status,
                        Total = x.Total,
                        ProductsCount = x.Products.Count,
                        PaidAt = x.PaidAt,
                        CreatedAt = x.CreatedAt
                    })
                    .Skip((rq.Page - 1) * Pagination.DefaultPageSize)
                    .Take(Pagination.DefaultPageSize)
                    .ToListAsync()
            };

            return rs;
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var cart = await _db
                .Cart
                .Include(x => x.User)
                .Include(x => x.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (cart == null)
                return rs.SetError(Messages.Error.EntityNotFound("Carrito"));

            if (!CanAccessCart(cart))
                return rs.SetError(Messages.Error.Unauthorized());

            rs.Data = MapCart(cart);
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();
            var token = _tokenService.GetToken();

            var cartProductsResponse = await BuildCartProducts(rq?.Products);
            if (cartProductsResponse.Error != null)
                return rs.SetError(cartProductsResponse.Error.Message);

            var cart = new Cart
            {
                UserId = token.UserId,
                Status = CartStatus.Open,
                Products = cartProductsResponse.Data,
                Total = CalculateTotal(cartProductsResponse.Data)
            };

            try
            {
                _db.Cart.Add(cart);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityCreated("Carrito");
            rs.Data = new CreateResponse
            {
                Id = cart.Id,
                Status = cart.Status,
                Total = cart.Total,
                Products = cart.Products.Select(MapCartProduct).ToList()
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var cart = await _db
                .Cart
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (cart == null)
                return rs.SetError(Messages.Error.EntityNotFound("Carrito"));

            if (!CanAccessCart(cart))
                return rs.SetError(Messages.Error.Unauthorized());

            if (!CanModifyCart(cart))
                return rs.SetError(Messages.Error.InvalidField("carrito"));

            var cartProductsResponse = await BuildCartProducts(rq.Products);
            if (!cartProductsResponse.Success)
                return rs.SetError(cartProductsResponse.Error.Message);

            foreach (var product in cart.Products)
            {
                product.DeletedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var product in cartProductsResponse.Data)
            {
                product.CartId = cart.Id;
                _db.CartProduct.Add(product);
            }

            cart.Total = CalculateTotal(cartProductsResponse.Data);
            cart.Status = CartStatus.Open;
            cart.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityUpdated("Carrito");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var cart = await _db
                .Cart
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (cart == null)
                return rs.SetError(Messages.Error.EntityNotFound("Carrito"));

            if (!CanAccessCart(cart))
                return rs.SetError(Messages.Error.Unauthorized());

            if (cart.Status == CartStatus.Paid)
                return rs.SetError(Messages.Error.InvalidField("carrito"));

            cart.Status = CartStatus.Cancelled;
            cart.DeletedAt = DateTime.UtcNow;

            foreach (var product in cart.Products)
            {
                product.DeletedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityDeleted("Carrito");
            return rs;
        }
        #endregion

        #region Validations
        private async Task<BaseResponse<List<CartProduct>>> BuildCartProducts(List<CartProductRequest> requestProducts)
        {
            var rs = new BaseResponse<List<CartProduct>>();

            if (requestProducts == null || requestProducts.Count == 0)
                return rs.SetError(Messages.Error.FieldRequired("productos"));

            if (requestProducts.Any(x => x == null || x.ProductId <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (requestProducts.Any(x => x.Quantity <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            var requestedProducts = requestProducts
                .GroupBy(x => x.ProductId)
                .Select(x => new
                {
                    ProductId = x.Key,
                    Quantity = x.Sum(p => p.Quantity)
                })
                .ToList();

            var productIds = requestedProducts.Select(x => x.ProductId).ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var requestedProduct in requestedProducts)
            {
                var product = products.FirstOrDefault(x => x.Id == requestedProduct.ProductId);
                if (product == null)
                    return rs.SetError(Messages.Error.EntityNotFound("Producto"));

                var validationError = ValidateProductForCart(product, requestedProduct.Quantity);
                if (!string.IsNullOrEmpty(validationError))
                    return rs.SetError(validationError);
            }

            rs.Data = requestedProducts
                .Select(x =>
                {
                    var product = products.First(p => p.Id == x.ProductId);

                    return new CartProduct
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = x.Quantity,
                        SettedPrice = product.Price
                    };
                })
                .ToList();

            return rs;
        }

        private static string ValidateProductForCart(Product product, int quantity)
        {
            if (!product.IsActive)
                return Messages.Error.InvalidField("producto");

            if (product.Price <= 0)
                return Messages.Error.FieldGraterThanZero("precio");

            if (product.Stock < quantity)
                return Messages.Error.ProductWithoutStock();

            return null;
        }
        #endregion

        #region Helpers
        private bool CanAccessCart(Cart cart)
        {
            return _authService.IsAdmin() || cart.UserId == _tokenService.GetToken().UserId;
        }

        private static bool CanModifyCart(Cart cart)
        {
            return cart.Status == CartStatus.Open;
        }

        private static IQueryable<Cart> FilterQuery(IQueryable<Cart> query, GetAllRequest rq)
        {
            if (!string.IsNullOrWhiteSpace(rq.Status))
                query = query.Where(x => x.Status == rq.Status.Trim());

            return query;
        }

        private static IQueryable<Cart> OrderQuery(IQueryable<Cart> query, PaginateRequest rq)
        {
            return rq.ColumnSort switch
            {
                "total" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Total) : query.OrderByDescending(x => x.Total),
                "status" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt),
            };
        }

        private static decimal CalculateTotal(List<CartProduct> products)
        {
            return products.Sum(x => x.Quantity * x.SettedPrice);
        }

        private static GetOneResponse MapCart(Cart cart)
        {
            return new GetOneResponse
            {
                Id = cart.Id,
                UserId = cart.UserId,
                UserEmail = cart.User?.Email,
                Status = cart.Status,
                Total = cart.Total,
                PaidAt = cart.PaidAt,
                CreatedAt = cart.CreatedAt,
                UpdatedAt = cart.UpdatedAt,
                Products = cart.Products
                    .OrderBy(x => x.ProductName)
                    .Select(MapCartProduct)
                    .ToList()
            };
        }

        private static CartProductResponse MapCartProduct(CartProduct product)
        {
            return new CartProductResponse
            {
                Id = product.Id,
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Quantity = product.Quantity,
                SettedPrice = product.SettedPrice,
                Total = product.Quantity * product.SettedPrice
            };
        }
        #endregion
    }
}
