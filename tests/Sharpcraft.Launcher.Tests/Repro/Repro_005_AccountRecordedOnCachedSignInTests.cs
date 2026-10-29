// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text;
using Sharpcraft.Auth;
using Sharpcraft.Launcher;
using Xunit;

namespace Sharpcraft.Launcher.Tests.Repro;

/// <summary>
/// Repro-005：`frame` 与 `window` 完成真实登录后并没有登记账号，于是"已经登录成功但离线闸门仍锁着"。
/// 修复是把登记动作放进登录流程本身，并覆盖"复用缓存会话"这条路径。
/// 关联：AGENTS 原则 7.1；`docs/modules/accounts.md`；`AuthenticationFlow`。
/// </summary>
public sealed class Repro_005_AccountRecordedOnCachedSignInTests
{
    [Fact]
    public void ACachedSignInStillReportsTheAccount()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-repro005-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new TokenStore(Path.Combine(root, "tokens.json"));
            store.Save(
                new MicrosoftToken("msa-access", "msa-refresh", DateTimeOffset.UtcNow.AddHours(1)),
                "service::user.auth.xboxlive.com::MBI_SSL",
                "client-id");
            store.SaveSession(
                new MinecraftSession("mc-access", DateTimeOffset.UtcNow.AddHours(12)),
                new MinecraftProfile("069a79f444e94726a5befca90e38aaf5", "Notch", Skins: true));

            var recorded = new List<(string Name, string Id)>();

            // Nothing may be requested: a valid cached session is the cheapest path.
            using var http = new HttpClient(new NeverRequestHandler());
            AuthenticationFlowProbe.SignIn(http, store, (name, id) => recorded.Add((name, id)));

            Assert.Single(recorded);
            Assert.Equal("Notch", recorded[0].Name);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class NeverRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"no request expected, got {request.RequestUri}");
    }
}
