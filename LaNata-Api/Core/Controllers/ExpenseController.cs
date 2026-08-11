using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Expense;
using LaNata_Api.Core.Services;
using LaNata_Api.Export.Models.Internal;

namespace LaNata_Api.Core.Controllers
{
    public class ExpenseController(ExpenseService expenseService) : BaseController
    {
        private readonly ExpenseService _expenseService = expenseService;

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _expenseService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _expenseService.GetOne(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _expenseService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Update([FromBody] UpdateRequest rq)
        {
            return await _expenseService.Update(rq);
        }

        [HttpPost]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _expenseService.Delete(rq);
        }
        #endregion

        #region Methods
        [HttpPost]
        public async Task<BaseResponse<GetBalanceResponse>> GetBalance([FromBody] GetBalanceRequest rq)
        {
            return await _expenseService.GetBalance(rq);
        }
        #endregion

        #region Documents
        [HttpPost]
        public async Task<IActionResult> ExportExcel([FromBody] GetAllRequest rq)
        {
            return DownloadOrError(await _expenseService.Export(ExportFileFormat.Excel, rq));
        }

        [HttpPost]
        public async Task<IActionResult> Report([FromBody] GetBalanceRequest rq)
        {
            var rs = await _expenseService.Report(rq);

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
