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

                    // プレビューでは元アセットを汚さないよう、常に複製してから編集する
                    var dst = Object.Instantiate(src);
                    dst.name = src.name;

                    bool applied = false;
                    foreach (var entry in overrides)
                    {
                        if (string.IsNullOrEmpty(entry.propertyName)) continue;
                        if (PropertyOverrideApplier.Apply(dst, entry))
                            applied = true;
                    }

                    // 1つも適用されなかったマテリアルは複製を捨てて元のまま使う
                    if (applied)
                        replacements[src] = dst;
                    else
                        Object.DestroyImmediate(dst);
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

            return context.Observe(
                component,
                c => c.overrides == null
                    ? new List<PropertyOverrideEntry>()
                    : c.overrides.Select(Clone).ToList(),
                (a, b) => a.Count == b.Count && a.Zip(b, EntryEquals).All(eq => eq));
        }

        /// <summary>
        /// Observe の比較用に、エントリの値をコピーする（参照のままだと変更を検出できない）。
        /// </summary>
        private static PropertyOverrideEntry Clone(PropertyOverrideEntry e)
        {
            return new PropertyOverrideEntry
            {
                propertyName = e.propertyName,
                propertyType = e.propertyType,
                floatValue   = e.floatValue,
                intValue     = e.intValue,
                colorValue   = e.colorValue,
                vector4Value = e.vector4Value,
            };
        }

        private static bool EntryEquals(PropertyOverrideEntry a, PropertyOverrideEntry b)
        {
            return a.propertyName == b.propertyName
                && a.propertyType == b.propertyType
                && a.floatValue.Equals(b.floatValue)
                && a.intValue == b.intValue
                && a.colorValue.Equals(b.colorValue)
                && a.vector4Value.Equals(b.vector4Value);
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
                _replacements.Clear();
            }
        }
    }
}
