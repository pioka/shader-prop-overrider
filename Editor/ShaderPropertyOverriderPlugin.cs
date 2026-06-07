using nadena.dev.ndmf;
using Pioka.ShaderPropOverrider.Editor;

[assembly: ExportsPlugin(typeof(ShaderPropertyOverriderPlugin))]

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// NDMFへのプラグイン登録。
    /// AAOが存在する場合は Optimizing フェーズで AAO より前に実行し、
    /// AAOの RemoveUnusedMaterialProperties が走る前にプロパティを確定させる。
    /// AAOが存在しない場合は Optimizing フェーズで順序制約なしに実行する。
    /// </summary>
    internal class ShaderPropertyOverriderPlugin : Plugin<ShaderPropertyOverriderPlugin>
    {
        public override string QualifiedName => "io.github.pioka.shader-prop-overrider";
        public override string DisplayName   => "Shader Property Overrider";

        protected override void Configure()
        {
#if HAS_AAO
            InPhase(BuildPhase.Optimizing)
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run(new ShaderPropertyOverriderPass());
#else
            InPhase(BuildPhase.Optimizing)
                .Run(new ShaderPropertyOverriderPass());
#endif
        }
    }
}
