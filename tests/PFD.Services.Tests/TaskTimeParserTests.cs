using NUnit.Framework;
using PFD.Services;
using PFD.Shared.Enums;

namespace PFD.Services.Tests;

[TestFixture]
public class TaskTimeParserTests
{
    [Test]
    [TestCase("meeting at 4:00 pm", "meeting", 16, 0)]
    [TestCase("meeting at 4:00 PM", "meeting", 16, 0)]
    [TestCase("meeting at 4:00pm", "meeting", 16, 0)]
    [TestCase("meeting at 9:30 am", "meeting", 9, 30)]
    [TestCase("meeting at 12:00 pm", "meeting", 12, 0)]
    [TestCase("meeting at 12:00 am", "meeting", 0, 0)]
    [TestCase("call client 3:00 pm", "call client", 15, 0)]
    [TestCase("standup 9am", "standup", 9, 0)]
    [TestCase("review 2pm", "review", 14, 0)]
    public void Parse_ExtractsTimeCorrectly(string input, string expectedTitle, int expectedHour, int expectedMinute)
    {
        var result = TaskTimeParser.Parse(input);

        Assert.That(result.CleanedTitle, Is.EqualTo(expectedTitle));
        Assert.That(result.ScheduledTime, Is.Not.Null);
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(expectedHour));
        Assert.That(result.ScheduledTime!.Value.Minutes, Is.EqualTo(expectedMinute));
    }

    [Test]
    [TestCase("schedule capstone meetings at 4:00 pm for Wed", 16, 0, RecurrenceType.Weekly, "Wed")]
    [TestCase("teach 333 MW 3:00 pm", 15, 0, RecurrenceType.Weekly, "Mon,Wed")]
    [TestCase("staff meeting TTh 10am", 10, 0, RecurrenceType.Weekly, "Tue,Thu")]
    [TestCase("daily standup 9am", 9, 0, RecurrenceType.Daily, null)]
    [TestCase("meeting MWF at 2:30 pm", 14, 30, RecurrenceType.Weekly, "Mon,Wed,Fri")]
    public void ParseWithRecurrence_ExtractsTimeAndRecurrence(
        string input,
        int expectedHour,
        int expectedMinute,
        RecurrenceType expectedRecurrence,
        string? expectedDays)
    {
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.ScheduledTime, Is.Not.Null, $"Time should be parsed from: {input}");
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(expectedHour), $"Hour mismatch for: {input}");
        Assert.That(result.ScheduledTime!.Value.Minutes, Is.EqualTo(expectedMinute), $"Minute mismatch for: {input}");
        Assert.That(result.RecurrenceType, Is.EqualTo(expectedRecurrence), $"Recurrence mismatch for: {input}");

        if (expectedDays != null)
        {
            Assert.That(result.RecurrenceDays, Is.Not.Null);
            Assert.That(string.Join(",", result.RecurrenceDays!), Is.EqualTo(expectedDays));
        }
    }

    [Test]
    [TestCase("teach 333 MW 3:00 pm till May 1")]
    [TestCase("meeting TTh 10am until Dec 15")]
    [TestCase("class MWF 9am through June 30")]
    public void ParseWithRecurrence_ExtractsEndDate(string input)
    {
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.RecurrenceEndDate, Is.Not.Null, $"End date should be parsed from: {input}");
    }

    [Test]
    [TestCase("organize 402 meetings")]
    [TestCase("buy groceries")]
    [TestCase("call mom")]
    public void Parse_NoTime_ReturnsNull(string input)
    {
        var result = TaskTimeParser.Parse(input);

        Assert.That(result.ScheduledTime, Is.Null);
        Assert.That(result.CleanedTitle, Is.EqualTo(input));
    }

    [Test]
    public void ParseWithRecurrence_ForWed_PreservesTimeAndDay()
    {
        // This is the specific failing case reported by user
        var input = "schedule capstone meetings at 4:00 pm for Wed";
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.ScheduledTime, Is.Not.Null, "Time should be parsed");
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(16), "Should be 4:00 PM = 16:00");
        Assert.That(result.ScheduledTime!.Value.Minutes, Is.EqualTo(0));
        Assert.That(result.RecurrenceType, Is.EqualTo(RecurrenceType.Weekly));
        Assert.That(result.RecurrenceDays, Contains.Item("Wed"));
    }

    [Test]
    [TestCase("meeting Wed at 4pm", RecurrenceType.Weekly, "Wed", 16)]
    [TestCase("meeting at 4pm Wed", RecurrenceType.Weekly, "Wed", 16)]
    [TestCase("meeting for Wed at 4pm", RecurrenceType.Weekly, "Wed", 16)]
    [TestCase("Wed meeting at 4pm", RecurrenceType.Weekly, "Wed", 16)]
    public void ParseWithRecurrence_DayAndTimeInDifferentOrders(
        string input,
        RecurrenceType expectedRecurrence,
        string expectedDay,
        int expectedHour)
    {
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.ScheduledTime, Is.Not.Null, $"Time should be parsed from: {input}");
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(expectedHour));
        Assert.That(result.RecurrenceType, Is.EqualTo(expectedRecurrence));
        Assert.That(result.RecurrenceDays, Contains.Item(expectedDay));
    }

    // ==================== DUE DATE PARSING TESTS ====================

    [Test]
    [TestCase("Prepare Week 5 by Feb 21", "Prepare Week 5", 2, 21)]
    [TestCase("Submit report by March 15", "Submit report", 3, 15)]
    [TestCase("Finish homework by 2/28", "Finish homework", 2, 28)]
    [TestCase("Review docs due by April 1", "Review docs", 4, 1)]
    public void ParseWithRecurrence_ExtractsDueDate(string input, string expectedTitle, int expectedMonth, int expectedDay)
    {
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.CleanedTitle, Is.EqualTo(expectedTitle), $"Title mismatch for: {input}");
        Assert.That(result.DueDate, Is.Not.Null, $"Due date should be parsed from: {input}");
        Assert.That(result.DueDate!.Value.Month, Is.EqualTo(expectedMonth), $"Month mismatch for: {input}");
        Assert.That(result.DueDate!.Value.Day, Is.EqualTo(expectedDay), $"Day mismatch for: {input}");
    }

    [Test]
    public void ParseWithRecurrence_DueDateAndTime_BothParsed()
    {
        var input = "Submit report by Feb 21 at 3pm";
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.CleanedTitle, Is.EqualTo("Submit report"));
        Assert.That(result.DueDate, Is.Not.Null);
        Assert.That(result.DueDate!.Value.Month, Is.EqualTo(2));
        Assert.That(result.DueDate!.Value.Day, Is.EqualTo(21));
        Assert.That(result.ScheduledTime, Is.Not.Null);
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(15));
    }

    [Test]
    public void ParseWithRecurrence_NoDueDate_ReturnsNull()
    {
        var input = "Regular task without deadline";
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.DueDate, Is.Null);
        Assert.That(result.CleanedTitle, Is.EqualTo(input));
    }

    // ==================== NAMED TIME PARSING TESTS ====================

    [Test]
    [TestCase("meet andrew at noon", "meet andrew", 12, 0)]
    [TestCase("meet andrew at noon tomorrow", "meet andrew tomorrow", 12, 0)]
    [TestCase("call at midnight", "call", 0, 0)]
    [TestCase("breakfast meeting at morning", "breakfast meeting", 9, 0)]
    [TestCase("dinner at evening", "dinner", 18, 0)]
    [TestCase("standup at lunchtime", "standup", 12, 0)]
    public void Parse_NamedTimes_ExtractsCorrectTime(string input, string expectedTitle, int expectedHour, int expectedMinute)
    {
        var result = TaskTimeParser.Parse(input);

        Assert.That(result.CleanedTitle, Is.EqualTo(expectedTitle), $"Title mismatch for: {input}");
        Assert.That(result.ScheduledTime, Is.Not.Null, $"Time should be parsed from: {input}");
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(expectedHour), $"Hour mismatch for: {input}");
        Assert.That(result.ScheduledTime!.Value.Minutes, Is.EqualTo(expectedMinute), $"Minute mismatch for: {input}");
    }

    [Test]
    public void ParseWithRecurrence_AtNoonTomorrow_ExtractsTime()
    {
        var input = "meet andrew at noon tomorrow";
        var result = TaskTimeParser.ParseWithRecurrence(input);

        Assert.That(result.ScheduledTime, Is.Not.Null, "Time should be parsed");
        Assert.That(result.ScheduledTime!.Value.Hours, Is.EqualTo(12), "Should be noon = 12:00");
        Assert.That(result.ScheduledTime!.Value.Minutes, Is.EqualTo(0));
    }
}
