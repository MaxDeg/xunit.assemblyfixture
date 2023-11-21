using System;

namespace Xunit;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AssemblyFixtureAttribute : Attribute
{
    public AssemblyFixtureAttribute(Type fixtureType) =>
        FixtureType = fixtureType;

    public Type FixtureType { get; }
}
