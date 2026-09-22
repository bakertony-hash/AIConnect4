namespace AIConnect4.Tests;

public class ModelCatalogTests
{
    [Fact]
    public void Defaults_are_jev_on_red_and_luna_on_yellow()
    {
        Assert.Equal("typesafe/jev-1.13", ModelCatalog.DefaultRed.ModelId);
        Assert.IsType<ModelProfile.SystemOne>(ModelCatalog.DefaultRed);
        Assert.Equal("openai/gpt-5.6-luna", ModelCatalog.DefaultYellow.ModelId);
        Assert.IsType<ModelProfile.Chat>(ModelCatalog.DefaultYellow);
    }

    [Fact]
    public void Curated_rows_carry_their_capability_flags()
    {
        var byId = ModelCatalog.Curated.ToDictionary(profile => profile.ModelId);

        Assert.IsType<ModelProfile.SystemOne>(byId["typesafe/jev-1.13"]);
        Assert.IsType<ModelProfile.SystemOne>(byId["~typesafe/jev-latest"]);
        var luna = Assert.IsType<ModelProfile.Chat>(byId["openai/gpt-5.6-luna"]);
        Assert.True(luna.SupportsEffort);
        Assert.True(luna.SupportsSpeed);
        Assert.Equal(AnswerFormat.JsonSchema, luna.AnswerFormat);
    }

    [Fact]
    public void Curated_rows_have_distinct_ids_and_names()
    {
        Assert.Equal(ModelCatalog.Curated.Length, ModelCatalog.Curated.Select(profile => profile.ModelId).Distinct().Count());
        Assert.Equal(ModelCatalog.Curated.Length, ModelCatalog.Curated.Select(profile => profile.DisplayName).Distinct().Count());
    }

    [Theory]
    [InlineData("typesafe/jev-1.13")]
    [InlineData("typesafe/jev-2")]
    [InlineData("~typesafe/jev-latest")]
    [InlineData("  ~typesafe/jev-latest  ")]
    public void Custom_typesafe_ids_are_system_one(string modelId)
    {
        var profile = Assert.IsType<ModelProfile.SystemOne>(ModelCatalog.Custom(modelId));

        Assert.Equal(modelId.Trim(), profile.ModelId);
        Assert.Equal(modelId.Trim(), profile.DisplayName);
    }

    [Theory]
    [InlineData("openai/gpt-5.6-sol")]
    [InlineData("mistralai/mistral-large")]
    [InlineData("nottypesafe/jev-1.13")]
    public void Custom_other_ids_are_chat_with_effort_and_speed_and_a_bare_number_answer(string modelId)
    {
        var profile = Assert.IsType<ModelProfile.Chat>(ModelCatalog.Custom(modelId));

        Assert.Equal(modelId, profile.ModelId);
        Assert.True(profile.SupportsEffort);
        Assert.True(profile.SupportsSpeed);
        Assert.Equal(AnswerFormat.Text, profile.AnswerFormat);
    }

    [Fact]
    public void Omit_tuning_sends_neither_effort_nor_speed()
    {
        Assert.Null(ChatTuning.Omit.Effort);
        Assert.Equal(Speed.Default, ChatTuning.Omit.Speed);
    }
}
