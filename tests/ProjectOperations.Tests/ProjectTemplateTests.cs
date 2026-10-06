using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class ProjectTemplateTests
{
    [Fact]
    public void BuiltInTemplateContainsTheExactSixteenRequirementsInFourGroups()
    {
        var template = VcTemplate.Create();
        Assert.Equal("VC Investment Review", template.Name);
        Assert.Equal(["business", "finance", "shareholders", "supplemental"], template.Groups.Select(group => group.Id));
        Assert.Equal(["اطلاعات کسب‌وکار", "اطلاعات مالی", "اطلاعات سهامداران", "اطلاعات تکمیلی"],
            template.Groups.Select(group => group.Title));
        Assert.Equal([6, 6, 3, 1], template.Groups.Select(group => group.Requirements.Count));
        Assert.Equal(new[]
        {
            "Pitch deck", "اطلاعات عملیات اجرایی / فرآیند اجرا", "فایل پیش‌بینی مالی",
            "سرمایه درخواستی و نحوه تخصیص سرمایه", "برنامه توسعه و چرایی جذب سرمایه",
            "تعداد مشتریان، قراردادهای فعال و قراردادهای در حال مذاکره", "صورت سود و زیان",
            "جریان وجوه نقد", "ترازنامه", "تراز کل / آزمایشی", "سوابق فروش و هزینه‌کرد ماهانه",
            "سوابق جذب سرمایه", "ترکیب سهامداران", "ترکیب تیم اجرایی", "سهام آزاد برای سرمایه‌گذاری", "مجوزها"
        }, template.InstantiateRequirements().Select(requirement => requirement.Title));
        Assert.Equal(16, template.InstantiateRequirements().Select(requirement => requirement.DefinitionId).Distinct().Count());
    }

    [Fact]
    public void InstantiationCreatesIndependentMutableSnapshotsAndIds()
    {
        var template = VcTemplate.Create();
        var first = template.InstantiateRequirements();
        var second = template.InstantiateRequirements();
        first[0].Title = "Changed";
        first[0].Status = RequirementStatus.Complete;
        first[0].Files.Add(new ProjectFile());
        Assert.Equal("Pitch deck", second[0].Title);
        Assert.Equal(RequirementStatus.Missing, second[0].Status);
        Assert.Empty(second[0].Files);
        Assert.NotEqual(first[0].Id, second[0].Id);
        Assert.Equal("Pitch deck", template.Groups[0].Requirements[0].Title);
        template.Groups[0].Title = "Changed";
        Assert.NotEqual("Changed", VcTemplate.Create().Groups[0].Title);
    }
}
