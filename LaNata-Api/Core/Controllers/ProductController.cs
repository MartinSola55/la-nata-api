using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Product;
using LaNata_Api.Core.Services;
using LaNata_Api.Export.Models.Internal;

namespace LaNata_Api.Core.Controllers
{
    public class ProductController(ProductService productService) : BaseController
    {
        private readonly ProductService _productService = productService;

        #region Combos
        [HttpGet]
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return await _productService.GetCombo();
        }
        #endregion

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _productService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _productService.GetOne(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _productService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _productService.Update(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _productService.Delete(rq);
        }
        #endregion

        #region Methods
        [HttpPost]
        public async Task<BaseResponse<GetStockResponse>> GetStock([FromBody] GetStockRequest rq)
        {
            return await _productService.GetStock(rq);
        }
        #endregion

        #region Documents
        // File endpoints are the one exception to returning BaseResponse: on success they return
        // the binary, and only fall back to the BaseResponse error shape when generation fails
        [HttpPost]
        public async Task<IActionResult> ExportCsv([FromBody] GetAllRequest rq)
        {
            return DownloadOrError(await _productService.Export(ExportFileFormat.Csv, rq));
        }

        [HttpPost]
        public async Task<IActionResult> ExportExcel([FromBody] GetAllRequest rq)
        {
            return DownloadOrError(await _productService.Export(ExportFileFormat.Excel, rq));
        }

        [HttpPost]
        public async Task<IActionResult> Report([FromBody] GetAllRequest rq)
        {
            var rs = await _productService.Report(rq);

            return rs.Success
                ? File(rs.Data.Content, rs.Data.ContentType, rs.Data.FileName)
                : BadRequest(rs);
        }

        private IActionResult DownloadOrError(BaseResponse<ExportFile> rs)
        {
            return rs.Success
                ? File(rs.Data.Content, rs.Data.ContentType, rs.Data.FileName)
                : BadRequest(rs);
        }
        #endregion
    }
}
