using System.Reflection;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Infrastructure.Agents;
using Xunit;

namespace ProjectOperations.Tests;

/// <summary>
/// External calendar events are other people's information and are display-only: they must never be stored with a project
/// or reach the agent. Enforced structurally so a later change cannot add the dependency without failing here.
/// </summary>
public sealed class ExternalCalendarBoundaryTests
{
    private static readonly HashSet<Type> CalendarTypes = [typeof(ExternalCalendarEvent), typeof(IExternalCalendarSource), typeof(NoExternalCalendar)];

    private static IEnumerable<Type> Mentioned(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element) foreach (var inner in Mentioned(element)) yield return inner;
        if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) foreach (var inner in Mentioned(argument)) yield return inner;
    }

    private static IEnumerable<Type> Dependencies(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var field in type.GetFields(all)) foreach (var mentioned in Mentioned(field.FieldType)) yield return mentioned;
        foreach (var property in type.GetProperties(all)) foreach (var mentioned in Mentioned(property.PropertyType)) yield return mentioned;
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            if (method is MethodInfo info) foreach (var mentioned in Mentioned(info.ReturnType)) yield return mentioned;
            foreach (var parameter in method.GetParameters()) foreach (var mentioned in Mentioned(parameter.ParameterType)) yield return mentioned;
        }
    }

    [Fact]
    public void Agent_and_project_types_do_not_depend_on_external_calendar_types()
    {
        var core = typeof(ExternalCalendarEvent).Assembly;
        var guarded = core.GetTypes()
            .Where(type => type.Namespace is not null && (type.Namespace.StartsWith("ProjectOperations.Core.Agents", StringComparison.Ordinal)
                || type.Namespace.StartsWith("ProjectOperations.Core.Domain", StringComparison.Ordinal)
                || type.Namespace.StartsWith("ProjectOperations.Core.Application", StringComparison.Ordinal)))
            .Concat(typeof(OpenCodeAgentRuntime).Assembly.GetTypes().Where(type => type.Namespace == typeof(OpenCodeAgentRuntime).Namespace))
            .ToList();

        Assert.NotEmpty(guarded); // the namespaces above must still match real types, or this test guards nothing
        var offenders = guarded.Where(type => Dependencies(type).Any(CalendarTypes.Contains)).Select(type => type.FullName).ToList();
        Assert.True(offenders.Count == 0, "These types depend on external calendar types: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_check_itself_can_detect_a_dependency()
    {
        Assert.Contains(typeof(ExternalCalendarEvent), Dependencies(typeof(Leak)).Where(CalendarTypes.Contains));
        Assert.Contains(typeof(ExternalCalendarEvent), Dependencies(typeof(LeakyList)).Where(CalendarTypes.Contains));
    }

    private sealed class Leak { public ExternalCalendarEvent? Event { get; set; } }
    private sealed class LeakyList { public void Add(IReadOnlyList<ExternalCalendarEvent> events) { _ = events; } }
}
