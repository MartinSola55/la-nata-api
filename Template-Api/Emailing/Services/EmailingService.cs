using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Emailing.Models;
using Template_Api.Emailing.Models.Internal;

namespace Template_Api.Emailing.Services
{
    public class EmailingService(APIContext context, IOptions<EmailOptions> opts)
    {
        private readonly EmailOptions _opts = opts.Value;
        private readonly APIContext _db = context;

        #region Methods
        public async Task<BaseResponse> SendAsync(EmailMessage message)
        {
            var sr = new BaseResponse();

            if (string.IsNullOrWhiteSpace(_opts.Host) || string.IsNullOrWhiteSpace(_opts.FromEmail))
                return sr.SetError(Messages.Error.EmailException());

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_opts.FromName, _opts.FromEmail));

            foreach (var to in message.To.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                mime.To.Add(MailboxAddress.Parse(to));

            mime.Subject = message.Subject ?? "";

            var body = await _db
                .EmailTemplate
                .Where(x => x.TemplateCode == message.TemplateCode)
                .Select(x => x.Html)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(body))
                return sr.SetError(Messages.Error.EmailTemplateNotFound());

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = RenderBody(body, message.Tokens)
            };

            foreach (var att in message.Attachments)
                bodyBuilder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));

            mime.Body = bodyBuilder.ToMessageBody();

            try
            {
                using var client = new SmtpClient();
                var secure = _opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

                await client.ConnectAsync(_opts.Host, _opts.Port, secure);

                if (!string.IsNullOrEmpty(_opts.User))
                    await client.AuthenticateAsync(_opts.User, _opts.Password);

                await client.SendAsync(mime);
                await client.DisconnectAsync(true);
            }
            catch (Exception)
            {
                return sr.SetError(Messages.Error.EmailException());
            }

            return sr;
        }
        #endregion

        #region Helpers
        public static string RenderBody(string template, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
                return template;

            var result = template;

            foreach (var kvp in tokens)
            {
                var placeholder = $"[{kvp.Key}]";
                result = result.Replace(placeholder, kvp.Value ?? string.Empty);
            }

            return result;
        }
        #endregion
    }
}
