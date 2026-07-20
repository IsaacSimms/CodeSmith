// == Metered AI endpoint attribute coverage == //
using System.Reflection;
using CodeSmith.Api.Authorization;
using CodeSmith.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Tests.Api;

/// <summary>
/// Pins which controller actions carry <see cref="MeteredAiAttribute"/> (auth + login_required)
/// and which authorized actions deliberately do not (billing).
/// </summary>
public class MeteredAiEndpointCoverageTests
{
    // == Expected metered AI actions (LLM-backed) == //

    public static TheoryData<Type, string> MeteredAiActions => new()
    {
        { typeof(SessionController),    nameof(SessionController.CreateSession) },
        { typeof(SessionController),    nameof(SessionController.CreateSessionStream) },
        { typeof(SessionController),    nameof(SessionController.Chat) },
        { typeof(SessionController),    nameof(SessionController.ChatStream) },
        { typeof(PromptLabController),  nameof(PromptLabController.StartChallenge) },
        { typeof(PromptLabController),  nameof(PromptLabController.SubmitAttempt) },
        { typeof(PromptLabController),  nameof(PromptLabController.Chat) },
        { typeof(PromptLabController),  nameof(PromptLabController.ChatStream) },
        { typeof(SystemLabController),  nameof(SystemLabController.StartSession) },
        { typeof(SystemLabController),  nameof(SystemLabController.SubmitAttempt) },
        { typeof(SystemLabController),  nameof(SystemLabController.Chat) },
        { typeof(SystemLabController),  nameof(SystemLabController.ChatStream) },
    };

    [Theory]
    [MemberData(nameof(MeteredAiActions))]
    public void MeteredAiActions_HaveMeteredAiAttribute(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);

        var attr = method.GetCustomAttribute<MeteredAiAttribute>();
        Assert.NotNull(attr);
        // MeteredAi subclasses AuthorizeAttribute — one attribute is the full seam
        Assert.IsAssignableFrom<AuthorizeAttribute>(attr);
    }

    // == Billing must stay generic [Authorize], not login_required metered == //

    public static TheoryData<string> BillingAuthorizedActions => new()
    {
        nameof(BillingController.CreateCheckout),
        nameof(BillingController.GetBalance),
        nameof(BillingController.GetLedger),
    };

    [Theory]
    [MemberData(nameof(BillingAuthorizedActions))]
    public void BillingAuthorizedActions_HaveAuthorizeButNotMeteredAi(string methodName)
    {
        var method = typeof(BillingController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(method.GetCustomAttribute<MeteredAiAttribute>());
    }

    // == Non-LLM practice actions stay untagged == //

    [Fact]
    public void SessionRunCode_DoesNotHaveMeteredAi()
    {
        var method = typeof(SessionController).GetMethod(nameof(SessionController.RunCode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);
        Assert.Null(method.GetCustomAttribute<MeteredAiAttribute>());
    }

    [Fact]
    public void CatalogGets_DoNotHaveMeteredAi()
    {
        Assert.Null(typeof(SessionController).GetMethod(nameof(SessionController.GetProviders))!.GetCustomAttribute<MeteredAiAttribute>());
        Assert.Null(typeof(PromptLabController).GetMethod(nameof(PromptLabController.GetChallenges))!.GetCustomAttribute<MeteredAiAttribute>());
        Assert.Null(typeof(SystemLabController).GetMethod(nameof(SystemLabController.GetScenarios))!.GetCustomAttribute<MeteredAiAttribute>());
    }
}
