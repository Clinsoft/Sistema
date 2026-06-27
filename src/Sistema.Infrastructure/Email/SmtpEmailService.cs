using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Infrastructure.Email;

public class SmtpEmailService(IConfiguration config) : IEmailService
{
    public async Task EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        var smtp     = config["Email:Smtp"]       ?? "";
        var porta    = int.Parse(config["Email:Porta"]    ?? "587");
        var usuario  = config["Email:Usuario"]    ?? "";
        var senha    = config["Email:Senha"]      ?? "";
        var remetente = config["Email:Remetente"] ?? "noreply@ecogranel.com";

        // Se SMTP não estiver configurado, apenas loga (dev)
        if (string.IsNullOrWhiteSpace(smtp) || string.IsNullOrWhiteSpace(usuario))
        {
            Console.WriteLine($"[EMAIL - SEM SMTP] Para: {destinatario} | Assunto: {assunto}");
            Console.WriteLine(corpoHtml);
            return;
        }

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(remetente));
        msg.To.Add(MailboxAddress.Parse(destinatario));
        msg.Subject = assunto;
        msg.Body = new TextPart("html") { Text = corpoHtml };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp, porta, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(usuario, senha, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
    }
}
