using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Generation;

/// <summary>Builds the whole generated tree from the pack, the rules and the skill template.</summary>
public static class TreeEmitter
{
    public static (GeneratedTree Tree, Manifest Manifest) Emit(PackConfig pack, IReadOnlyList<Rule> rules, string skillTemplate)
    {
        var tree = EmitContent(pack, pack.Version, rules, skillTemplate);
        var contentHash = ContentHash.Compute(EmitContent(pack, ContentHash.SentinelVersion, rules, skillTemplate));
        var manifest = Manifest.Create(pack, pack.Version, contentHash, rules);
        tree.Add(GeneratedPaths.Manifest, manifest.ToJson());
        return (tree, manifest);
    }

    private static GeneratedTree EmitContent(PackConfig pack, string version, IReadOnlyList<Rule> rules, string skillTemplate)
    {
        var tree = new GeneratedTree();
        SkillEmitter.Emit(tree, pack, version, rules, skillTemplate);
        AnalyzerConfigEmitter.Emit(tree, version, rules);
        PackageEmitter.Emit(tree, pack, version, rules);
        return tree;
    }
}
