using nadena.dev.ndmf;
using Pioka.ShaderPropOverrider.Editor;

[assembly: ExportsPlugin(typeof(ShaderPropertyOverriderPlugin))]

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// NDMFへのプラグイン登録。
    /// Optimizing フェーズで AAO より前に実行し、AAOの RemoveUnusedMaterialProperties が
    /// 走る前にプロパティを確定させる。
    /// BeforePlugin は対象プラグイン未導入時は無視されるため、AAO の有無にかかわらず安全。
    /// </summary>
    internal class ShaderPropertyOverriderPlugin : Plugin<ShaderPropertyOverriderPlugin>
    {
        public override string QualifiedName => "io.github.pioka.shader-prop-overrider";
        public override string DisplayName   => "Shader Property Overrider";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run(new ShaderPropertyOverriderPass());
        }
    }
}
