using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.User;
using Template_Api.Core.Services;

namespace Template_Api.Core.Controllers
{
    public class UserController(UserService userService) : BaseController
    {
        private readonly UserService _userService = userService;

        #region Combos
        [HttpGet]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse<BaseComboResponse>> GetComboRoles()
        {
            return await _userService.GetComboRoles();
        }

        [HttpGet]
        public async Task<BaseResponse<BaseComboResponse>> GetComboUsers([FromQuery] GetComboUsersRequest rq)
        {
            return await _userService.GetComboUsers(rq);
        }
        #endregion

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _userService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _userService.GetOne(rq);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse<CreateResponse>> Create([FromBody] CreateRequest rq)
        {
            return await _userService.Create(rq);
        }

        [HttpPost]
        public async Task<BaseResponse<UpdateResponse>> Update([FromBody] UpdateRequest rq)
        {
            return await _userService.Update(rq);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Admin)]
        public async Task<BaseResponse> Delete([FromBody] DeleteRequest rq)
        {
            return await _userService.Delete(rq);
        }
        #endregion

        #region Methods
        [HttpPost]
        public async Task<BaseResponse> UpdatePassword([FromBody] UpdatePasswordRequest rq)
        {
            return await _userService.UpdatePassword(rq);
        }
        #endregion
    }
}
