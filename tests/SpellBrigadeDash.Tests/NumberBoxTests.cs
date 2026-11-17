namespace SpellBrigadeDash.Tests;

using System;
using SpellBrigadeDash.Core;
using Xunit;

/// <summary>
/// HP is written through reflection (PropertyInfo.SetValue), which needs the boxed value to be exactly the member's
/// type. 0.3.2 boxed a float as a double (a ?: chain widens every branch to double) and the HP cost never applied.
/// </summary>
public class NumberBoxTests
{
    [Fact]
    public void A_float_member_gets_a_boxed_float()
    {
        var boxed = NumberBox.As(570f, typeof(float));
        Assert.IsType<float>(boxed);
        Assert.Equal(570f, (float)boxed);
    }

    [Fact]
    public void An_int_member_gets_a_boxed_int()
    {
        var boxed = NumberBox.As(570.6f, typeof(int));
        Assert.IsType<int>(boxed);
        Assert.Equal(570, (int)boxed);
    }

    [Fact]
    public void A_double_member_gets_a_boxed_double()
    {
        var boxed = NumberBox.As(570.5f, typeof(double));
        Assert.IsType<double>(boxed);
        Assert.Equal(570.5, (double)boxed, 3);
    }

    [Fact]
    public void Other_types_are_refused()
    {
        Assert.Throws<ArgumentException>(() => NumberBox.As(1f, typeof(string)));
    }
}
