using FluentAssertions;
using ReportingHub.Api.Contracts.Enums;
using ReportingHub.Api.Infrastructure.Parsing;

namespace ReportingHub.Api.Tests.Parsing;

public class NamesRulesParserTests
{
    [Fact]
    public void Parse_Empty_ReturnsDefaults()
    {
        var r1 = NamesRulesParser.Parse(null);
        var r2 = NamesRulesParser.Parse(string.Empty);
        var r3 = NamesRulesParser.Parse("   ");

        foreach (var r in new[] { r1, r2, r3 })
        {
            r.Mode.Should().Be(MergeMode.Pattern);
            r.Collision.Should().Be(CollisionPolicy.Dedupe);
            r.Pattern.Should().Be("{file}_{sheet}");
            r.Map.Should().BeNull();
        }
    }

    [Fact]
    public void Parse_Pattern_WithDedupe_Valid()
    {
        var json = """{ "mode": "pattern", "pattern": "{sheet}", "collision": "dedupe" }""";

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
    }

    [Fact]
    public void Parse_Pattern_WithError_Valid()
    {
        var json = """{ "mode": "pattern", "pattern": "{file}-{sheet}", "collision": "error" }""";

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{file}-{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Error);
    }

    [Fact]
    public void Parse_Pattern_CaseInsensitivity_ForEnum()
    {
        var json = """{ "mode": "PaTtErN", "pattern": "{sheet}", "collision": "ErRoR" }""";

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Collision.Should().Be(CollisionPolicy.Error);
    }

    [Fact]
    public void Parse_Pattern_MissingPattern_FallsBackToDefault()
    {
        var json = """{ "mode": "pattern", "collision": "dedupe" }""";

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{file}_{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
    }

    [Fact]
    public void Parse_Map_ExplicitMapping_Works()
    {
        var json = """
        {
          "mode": "map",
          "collision": "dedupe",
          "map": {
            "A.xlsx": { "Sheet1": "GrantsFY25" },
            "B.xlsx": { "*": "{file}-{sheet}" }
          }
        }
        """;

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Map);
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
        r.Map.Should().NotBeNull();
        r.Map!.Should().ContainKey("A.xlsx");
        r.Map!["A.xlsx"].Should().Contain(new KeyValuePair<string, string>("Sheet1", "GrantsFY25"));
        r.Map!.Should().ContainKey("B.xlsx");
        r.Map!["B.xlsx"].Should().Contain(new KeyValuePair<string, string>("*", "{file}-{sheet}"));
    }

    [Fact]
    public void Parse_Map_WithoutMap_FallsBackToPatternDefaults()
    {
        var jsonNoMap = """{ "mode": "map", "collision": "dedupe" }""";
        var jsonEmptyMap = """{ "mode": "map", "collision": "dedupe", "map": { } }""";

        foreach (var json in new[] { jsonNoMap, jsonEmptyMap })
        {
            var r = NamesRulesParser.Parse(json);

            r.Mode.Should().Be(MergeMode.Pattern);        // fallback
            r.Pattern.Should().Be("{file}_{sheet}");      // default
            r.Collision.Should().Be(CollisionPolicy.Dedupe);
            r.Map.Should().BeNull();
        }
    }

    [Fact]
    public void Parse_BadJson_FallsBackToDefaults()
    {
        var r = NamesRulesParser.Parse("{ invalid json }");

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{file}_{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
        r.Map.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownProperties_AreIgnored()
    {
        var json = """
        {
          "mode": "pattern",
          "pattern": "{sheet}",
          "collision": "dedupe",
          "unknownProp": "ignored",
          "map": { "A.xlsx": { "Sheet1": "X" } }
        }
        """;

        var r = NamesRulesParser.Parse(json);

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
        // When mode==pattern, map is allowed to exist in JSON but shouldn't be required
    }

    [Fact]
    public void Parse_Whitespace_IsTreatedAsEmpty()
    {
        var r = NamesRulesParser.Parse("   \r\n\t ");

        r.Mode.Should().Be(MergeMode.Pattern);
        r.Pattern.Should().Be("{file}_{sheet}");
        r.Collision.Should().Be(CollisionPolicy.Dedupe);
    }
}
