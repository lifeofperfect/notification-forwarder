using System.Reflection;
using NetArchTest.Rules;
using NotificationForwarder.Application;
using NotificationForwarder.Infrastructure;

namespace NotificationForwarder.UnitTests.Architecture;

/// <summary>The dependency direction is Domain &lt;- Application &lt;- Infrastructure &lt;- Api, and nothing may point the other way.</summary>
public sealed class CleanArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(NotificationEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ApplicationDependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureDependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void DomainShouldNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Name(ApplicationAssembly), Name(InfrastructureAssembly), Name(ApiAssembly))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void ApplicationShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Name(InfrastructureAssembly), Name(ApiAssembly))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void InfrastructureShouldNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(Name(ApiAssembly))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    private static string Name(Assembly assembly) => assembly.GetName().Name!;

    private static string Explain(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful ? string.Empty : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
