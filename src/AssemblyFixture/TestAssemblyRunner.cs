using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit;

class TestAssemblyRunner : XunitTestAssemblyRunner
{
    readonly Dictionary<Type, object> assemblyFixtureMappings = new();

    public TestAssemblyRunner(ITestAssembly testAssembly,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
        : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
    }

    protected override async Task AfterTestAssemblyStartingAsync()
    {
        await base.AfterTestAssemblyStartingAsync();

        // Go find all the AssemblyFixtureAttributes adorned on the test assembly
        Aggregator.Run(() =>
        {
            var fixturesAttrs = ((IReflectionAssemblyInfo)TestAssembly.Assembly).Assembly
                .GetCustomAttributes(typeof(AssemblyFixtureAttribute), false)
                .Cast<AssemblyFixtureAttribute>()
                .ToList();

            // Instantiate all the fixtures
            foreach (var fixtureAttr in fixturesAttrs)
                assemblyFixtureMappings[fixtureAttr.FixtureType] = Activator.CreateInstance(fixtureAttr.FixtureType);
        });

        // Call InitializeAsync on all instances of IAssemblyAsyncLifetime, and use Aggregator.RunAsync to isolate
        // InitializeAsync failures
        foreach (var disposable in assemblyFixtureMappings.Values.OfType<IAsyncLifetime>())
            await Aggregator.RunAsync(disposable.InitializeAsync);
    }

    protected override async Task BeforeTestAssemblyFinishedAsync()
    {
        // Call DisposeAsync on all instances of IAssemblyAsyncLifetime, and use Aggregator.RunAsync to isolate
        // DisposeAsync failures
        foreach (var disposable in assemblyFixtureMappings.Values.OfType<IAsyncLifetime>())
            await Aggregator.RunAsync(disposable.DisposeAsync);

        await base.BeforeTestAssemblyFinishedAsync();
    }

    protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus,
        ITestCollection testCollection,
        IEnumerable<IXunitTestCase> testCases,
        CancellationTokenSource cancellationTokenSource)
        => new TestCollectionRunner(assemblyFixtureMappings, testCollection, testCases, DiagnosticMessageSink,
            messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
}