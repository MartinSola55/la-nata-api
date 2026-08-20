using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Category;
using LaNata_Api.Core.Services;

namespace LaNata_Api.Core.Controllers
{
    public class CategoryController(CategoryService categoryService) : BaseController
    {
        private readonly CategoryService _categoryService = categoryService;

        #region Combos
        [HttpGet]
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return await _categoryService.GetCombo();
        }
        #endregion

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _categoryService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _categoryService.GetOne(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _categoryService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _categoryService.Update(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _categoryService.Delete(rq);
        }
        #endregion
    }
}
