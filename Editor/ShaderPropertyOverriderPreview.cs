using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// NDMF Preview 用のレンダーフィルタ。
    /// シーン上のアバターには触れず、プレビュー用のプロキシ Renderer のマテリアルだけを
    /// 複製・上書きして、ビルド結果と同じ見た目をエディタ上に表示する。
    /// </summary>
    internal class ShaderPropertyOverriderPreview : IRenderFilter
    {
        /// <summary>
        /// コンポーネントを持つアバターごとに、配下のプレビュー対応 Renderer を1グループとして返す。
        /// ビルド時と同様に「同一マテリアルは1回だけ複製する」ため、アバター単位でまとめる。
        /// </summary>
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var groups = ImmutableList.CreateBuilder<RenderGroup>();

            foreach (var root in context.GetAvatarRoots())
            {
                var components = context.GetComponentsInChildren<ShaderPropertyOverrider>(root, true);
                if (components.Length == 0) continue;

                // NDMF Preview は MeshRenderer / SkinnedMeshRenderer 以外を含むと
                // フィルタ全体を無効化するため、それ以外（パーティクル等）は除外する
                var renderers = context.GetComponentsInChildren<Renderer>(root, true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                    .ToArray();
                if (renderers.Length == 0) continue;

                groups.Add(RenderGroup.For(renderers));
            }

            return groups.ToImmutable();
        }

        public Task<IRenderFilterNode> Instantiate(
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            var overrides = ObserveOverrides(group, context);

            // キー: プロキシが持っていたマテリアル、値: 上書き済みの複製（上書きが当たったものだけ）
            var replacements = new Dictionary<Material, Material>();
            var visited = new HashSet<Material>();

            foreach (var (_, proxy) in proxyPairs)
            {
                foreach (var src in proxy.sharedMaterials)
                {
                    if (src == null || !visited.Add(src)) continue;

                    // マテリアル側の編集にも追従する
                    context.Observe(src);

                    // プレビューでは元アセットを汚さないよう、常に複製してから編集する。
                    // 上書きが1つも当たらないマテリアルは差し替えない。
                    if (PropertyOverrideApplier.TryCreateOverriddenCopy(src, overrides, null, out var dst))
                        replacements[src] = dst;
                }
            }

            return Task.FromResult<IRenderFilterNode>(new Node(replacements));
        }

        /// <summary>
        /// グループが属するアバターのコンポーネントを取得し、上書きエントリのスナップショットを返す。
        /// エントリの値が変わるとプレビューが再計算される。
        /// </summary>
        private static List<PropertyOverrideEntry> ObserveOverrides(RenderGroup group, ComputeContext context)
        {
            var root = context.GetAvatarRoot(group.Renderers[0].gameObject);
            if (root == null) return new List<PropertyOverrideEntry>();

            // ビルド時のパスと同じく、最初に見つかったコンポーネントを使う
            var component = context.GetComponentsInChildren<ShaderPropertyOverrider>(root, true)
                .FirstOrDefault();
            if (component == null) return new List<PropertyOverrideEntry>();

            // シリアライズ結果の文字列で比較し、エントリの内容が変わったときだけ再計算させる。
            // Instantiate は同期的に完了しエントリを保持しないため、適用には実体をそのまま使う。
            context.Observe(component, c => JsonUtility.ToJson(c));
            return component.overrides;
        }

        /// <summary>
        /// 複製したマテリアルを保持し、毎フレームプロキシに差し替える。ノード破棄時に解放する。
        /// NDMF はフレームごとに元 Renderer の状態（マテリアル含む）をプロキシへコピーし直すため、
        /// Instantiate で差し替えるだけでは反映されず、OnFrame で毎回差し替える必要がある。
        /// </summary>
        private class Node : IRenderFilterNode
        {
            private readonly Dictionary<Material, Material> _replacements;

            public RenderAspects WhatChanged => RenderAspects.Material;

            public Node(Dictionary<Material, Material> replacements)
            {
                _replacements = replacements;
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (proxy == null || _replacements.Count == 0) return;

                var materials = proxy.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var src = materials[i];
                    if (src == null) continue;

                    if (_replacements.TryGetValue(src, out var dst))
                    {
                        materials[i] = dst;
                        modified = true;
                    }
                }

                if (modified)
                    proxy.sharedMaterials = materials;
            }

            public void Dispose()
            {
                foreach (var material in _replacements.Values)
                {
                    if (material != null)
                        Object.DestroyImmediate(material);
                }
            }
        }
    }
}
