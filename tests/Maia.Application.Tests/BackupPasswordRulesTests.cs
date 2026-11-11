using Maia.Application.Plans;

namespace Maia.Application.Tests;

public sealed class BackupPasswordRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("sea-bree9")]
    [InlineData("θάλασσα-1")]
    public void Fewer_than_ten_characters_are_too_short(string password) =>
        Assert.Equal(BackupPasswordIssue.TooShort, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("aaaaaaaaaa")]
    [InlineData("ababababababab")]
    [InlineData("passpasspass")]
    [InlineData("abc abc abc abc")]
    [InlineData("1111122222")]
    [InlineData("αααααααααααα")]
    public void The_same_few_characters_over_and_over_are_turned_away(string password) =>
        Assert.Equal(BackupPasswordIssue.Repeated, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("abcdefghijk")]
    [InlineData("0123456789")]
    [InlineData("9876543210")]
    [InlineData("qwertyuiop")]
    [InlineData("QWERTYUIOPASDF")]
    [InlineData("1q2w3e4r5t")]
    [InlineData("ςερτυθιοπασ")]
    [InlineData("αβγδεζηθικ")]
    public void Runs_of_letters_digits_or_keys_are_turned_away(string password) =>
        Assert.Equal(BackupPasswordIssue.Sequence, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("17/01/2027")]
    [InlineData("17.01.2027")]
    [InlineData("6912345678")]
    [InlineData("210 555 0137")]
    [InlineData("3069123456781")]
    public void Dates_and_phone_numbers_are_turned_away(string password) =>
        Assert.Equal(BackupPasswordIssue.DigitsOnly, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("1234567890!")]
    [InlineData("qwertyuiop!")]
    [InlineData("abcdefghij!")]
    [InlineData("!!0987654321")]
    [InlineData("qwertyuiop12")]
    [InlineData("x1234567890")]
    [InlineData("#ασδφγηξκλ#")]
    public void A_run_with_a_symbol_or_digit_added_is_still_a_run(string password) =>
        Assert.Equal(BackupPasswordIssue.Sequence, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("01234567890123")]
    [InlineData("1234567890123456")]
    [InlineData("78901234567890")]
    [InlineData("qwertyuiopqwerty")]
    [InlineData("asdfghjklasdfg")]
    [InlineData("xyzabcdefghij")]
    public void A_run_that_goes_round_again_is_still_a_run(string password) =>
        Assert.Equal(BackupPasswordIssue.Sequence, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("17/01/2027!!")]
    [InlineData("!!17012027")]
    [InlineData("+306912345678")]
    [InlineData("(210) 555-0137")]
    [InlineData("#17-01-2027#")]
    public void Dates_and_phone_numbers_with_symbols_are_still_numbers_alone(string password) =>
        Assert.Equal(BackupPasswordIssue.DigitsOnly, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("passwordpassword1")]
    [InlineData("1maia1maia1maia")]
    [InlineData("!summersummer!")]
    [InlineData("27babybaby")]
    public void A_repeat_with_a_few_characters_added_is_still_a_repeat(string password) =>
        Assert.Equal(BackupPasswordIssue.Repeated, BackupPasswordRules.Check(password));

    [Fact]
    public void Fourteen_digits_or_more_are_enough() =>
        Assert.Equal(BackupPasswordIssue.None, BackupPasswordRules.Check("30691234567819"));

    [Theory]
    [InlineData("password123")]
    [InlineData("Password2027!")]
    [InlineData("P@ssw0rd2027")]
    [InlineData("iloveyou2026")]
    [InlineData("maia20272027")]
    [InlineData("pregnancy1!")]
    [InlineData("agapimou123")]
    [InlineData("Αγάπη μου 2027")]
    [InlineData("ΚΩΔΙΚΟΣ12345")]
    [InlineData("2027-mybaby!")]
    public void Passwords_people_pick_first_are_turned_away(string password) =>
        Assert.Equal(BackupPasswordIssue.Common, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("sea-breeze-17")]
    [InlineData("olive tree morning")]
    [InlineData("θάλασσα-17-ήλιος")]
    [InlineData("Kalamata in May")]
    public void A_few_words_she_will_remember_pass(string password) =>
        Assert.Equal(BackupPasswordIssue.None, BackupPasswordRules.Check(password));

    [Theory]
    [InlineData("sea-breeze-17", false)]
    [InlineData("olive tree morning", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("1234567890123456", false)]
    [InlineData("qwertyuiopqwerty", false)]
    [InlineData("passwordpassword1", false)]
    [InlineData("θάλασσα-17-ήλιος-Μάιος", true)]
    public void Long_acceptable_passwords_count_as_strong(string password, bool strong) =>
        Assert.Equal(strong, BackupPasswordRules.IsStrong(password));

    [Fact]
    public void A_run_of_symbol_keys_is_a_run() =>
        Assert.Equal(BackupPasswordIssue.Sequence, BackupPasswordRules.Check("!@#$%^&*()"));

    [Fact]
    public void Separators_and_spaces_alone_are_turned_away() =>
        Assert.Equal(BackupPasswordIssue.Repeated, BackupPasswordRules.Check(".-/_,:\\ \t  "));

    [Theory]
    [InlineData("red fig 42")]
    [InlineData("blue-sky fig")]
    public void A_few_short_words_with_spaces_between_them_pass(string password) =>
        Assert.Equal(BackupPasswordIssue.None, BackupPasswordRules.Check(password));
}
