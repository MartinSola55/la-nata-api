using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.GenericItem;
using Template_Api.Core.Services;

namespace Template_Api.Core.Controllers
{
    public class GenericItemController(GenericItemService genericItemService) : BaseController
    {
        private readonly GenericItemService _genericItemService = genericItemService;

        #region Combos
        [HttpGet]
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return await _genericItemService.GetCombo();
        }
        #endregion

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _genericItemService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _genericItemService.GetOne(rq);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _genericItemService.Create(rq);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _genericItemService.Update(rq);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _genericItemService.Delete(rq);
        }
        #endregion

        #region Methods
        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse> Activate([FromBody] ActivateRequest rq)
        {
            return await _genericItemService.Activate(rq);
        }
        #endregion
    }
}
