using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Order;
using LaNata_Api.Core.Services;
using LaNata_Api.Export.Models.Internal;

namespace LaNata_Api.Core.Controllers
{
    public class OrderController(OrderService orderService) : BaseController
    {
        private readonly OrderService _orderService = orderService;

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _orderService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _orderService.GetOne(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _orderService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _orderService.Update(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _orderService.Delete(rq);
        }
        #endregion

        #region Methods
        [HttpPost]
        public async Task<BaseResponse<GetDatesResponse>> GetDates([FromBody] GetDatesRequest rq)
        {
            return await _orderService.GetDates(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> AddProduct([FromBody] AddProductRequest rq)
        {
            return await _orderService.AddProduct(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> RemoveProduct([FromBody] RemoveProductRequest rq)
        {
            return await _orderService.RemoveProduct(rq);
        }
        #endregion

        #region Documents
        [HttpPost]
        public async Task<IActionResult> ExportCsv([FromBody] GetAllRequest rq)
        {
            return DownloadOrError(await _orderService.Export(ExportFileFormat.Csv, rq));
        }

        [HttpPost]
        public async Task<IActionResult> ExportExcel([FromBody] GetAllRequest rq)
        {
            return DownloadOrError(await _orderService.Export(ExportFileFormat.Excel, rq));
        }

        [HttpPost]
        public async Task<IActionResult> Report([FromBody] ReportRequest rq)
        {
            var rs = await _orderService.Report(rq);

            return rs.Success
                ? File(rs.Data.Content, rs.Data.ContentType, rs.Data.FileName)
                : BadRequest(rs);
        }

        [HttpPost]
        public async Task<IActionResult> Budget([FromBody] BudgetRequest rq)
        {
            var rs = await _orderService.Budget(rq);

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
