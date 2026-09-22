namespace AIConnect4.Tests;

public sealed class ColumnTests
{
    [Fact]
    public void TryFrom_rejects_0_and_8()
    {
        Assert.Null(Column.TryFrom(0));
        Assert.Null(Column.TryFrom(8));
    }

    [Fact]
    public void TryFrom_1_has_value_1_and_TryFrom_7_has_index_6()
    {
        Assert.Equal(1, Column.TryFrom(1)?.Value);
        Assert.Equal(6, Column.TryFrom(7)?.Index);
    }

    [Fact]
    public void TryParse_trims_digits_and_rejects_other_text()
    {
        Assert.Equal(4, Column.TryParse(" 4 ")?.Value);
        Assert.Null(Column.TryParse("x"));
    }

    [Fact]
    public void All_lists_1_through_7_in_order()
    {
        int[] expected = [1, 2, 3, 4, 5, 6, 7];

        Assert.Equal(expected, Column.All.Select(column => column.Value));
    }

    [Fact]
    public void Default_column_is_column_1()
    {
        Assert.Equal(1, default(Column).Value);
    }
}
