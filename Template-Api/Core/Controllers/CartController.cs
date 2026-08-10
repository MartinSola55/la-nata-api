using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.Cart;
using Template_Api.Core.Services;

namespace Template_Api.Core.Controllers
{
    public class CartController(CartService cartService) : BaseController
    {
        private readonly CartService _cartService = cartService;

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _cartService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _cartService.GetOne(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _cartService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _cartService.Update(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _cartService.Delete(rq);
        }
        #endregion
    }
}
