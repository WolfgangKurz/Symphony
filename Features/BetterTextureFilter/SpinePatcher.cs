using Spine.Unity;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Symphony.Features.BetterTextureFilter {
	[DisallowMultipleComponent]
	internal sealed class SpinePatcher : MonoBehaviour {
		private enum SourceStatus {
			Waiting,
			Pending,
			Ready,
			Failed
		}

		private sealed class RendererState {
			internal readonly int index;
			internal readonly SkeletonRenderer skeletonRenderer;
			internal readonly MeshRenderer meshRenderer;
			internal readonly SkeletonRenderSeparator separator;
			internal readonly Dictionary<Material, Material> overrides;
			internal readonly Dictionary<Material, MaterialBinding> bindings = new();
			internal readonly List<Material> materialBuffer = new();
			internal readonly List<int> materialSignature = new();
			internal readonly List<Material> staleBindings = new();
			internal readonly List<Material> retiredMaterials = new();
			internal readonly List<int> separatorSignature = new();
			internal readonly List<int> separatorSignatureBuffer = new();

			internal RendererState(int index, SkeletonRenderer skeletonRenderer) {
				this.index = index;
				this.skeletonRenderer = skeletonRenderer;
				meshRenderer = skeletonRenderer.GetComponent<MeshRenderer>();
				separator = skeletonRenderer.GetComponent<SkeletonRenderSeparator>();
				overrides = skeletonRenderer.CustomMaterialOverride;
			}
		}

		private sealed class MaterialBinding {
			internal readonly RendererState renderer;
			internal readonly Material original;
			internal readonly Material previousOverride;
			internal readonly bool hadPreviousOverride;
			internal readonly Material clone;
			internal readonly SourceWork source;
			internal readonly string discriminator;

			internal MaterialBinding(
				RendererState renderer,
				Material original,
				Material previousOverride,
				bool hadPreviousOverride,
				Material clone,
				SourceWork source,
				string discriminator
			) {
				this.renderer = renderer;
				this.original = original;
				this.previousOverride = previousOverride;
				this.hadPreviousOverride = hadPreviousOverride;
				this.clone = clone;
				this.source = source;
				this.discriminator = discriminator;
			}
		}

		private sealed class SourceWork {
			internal readonly int instanceId;
			internal readonly Texture2D source;
			internal readonly List<MaterialBinding> bindings = new();
			internal string discriminator;
			internal SourceStatus status;
			internal Texture2D replacement;

			internal SourceWork(Texture2D source, string discriminator) {
				this.source = source;
				this.discriminator = discriminator;
				instanceId = source.GetInstanceID();
			}

			internal void RegisterDiscriminator(string value) {
				if (
					status == SourceStatus.Waiting &&
					(discriminator == null || string.CompareOrdinal(value, discriminator) < 0)
				)
					discriminator = value;
			}
		}

		private readonly Dictionary<int, RendererState> renderers = new();
		private readonly Dictionary<int, SourceWork> sources = new();
		private readonly HashSet<int> ownedMaterialIds = new();
		private readonly List<Material> ownedMaterials = new();
		private readonly List<Texture2D> ownedTextures = new();

		private static MonoBehaviour coroutineRunner;

		private string bundleName;
		private string assetName;
		private Hash128 bundleVersion;
		private bool initialized;
		private bool disposed;
		private bool discovering;

		internal string BundleName => bundleName;
		internal string AssetName => assetName;
		internal Hash128 BundleVersion => bundleVersion;

		internal static void SetCoroutineRunner(MonoBehaviour runner) {
			coroutineRunner = runner;
		}

		internal static void AttachAll(string bundleName, string assetName, GameObject root) {
			if (!root) return;

			try {
				foreach (var actor in root.GetComponentsInChildren<Actor_Spine>(true)) {
					if (!actor) continue;

					try {
						var patcher = actor.GetComponent<SpinePatcher>();
						if (!patcher) {
							patcher = actor.gameObject.AddComponent<SpinePatcher>();
							patcher.hideFlags = HideFlags.HideInInspector;
						}

						patcher.Initialize(bundleName, assetName);
					} catch (Exception exception) {
						Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to initialize Spine texture patch on '{actor.name}': {exception}");
					}
				}
			} catch (Exception exception) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to find Spine actors on '{root.name}': {exception}");
			}
		}

		internal static bool IsSpineRenderer(MeshRenderer renderer) {
			if (!renderer) return false;

			var patcher = renderer.GetComponentInParent<SpinePatcher>(true);
			return patcher && patcher.ManagesRenderer(renderer);
		}

		private bool ManagesRenderer(MeshRenderer renderer) {
			foreach (var state in renderers.Values) {
				if (state.meshRenderer == renderer) return true;
				if (!state.separator) continue;

				foreach (var part in state.separator.partsRenderers)
					if (part && part.MeshRenderer == renderer) 
						return true;
			}

			return false;
		}

		private void Initialize(string bundleName, string assetName) {
			bundleName = bundleName?.ToLowerInvariant() ?? "";
			assetName ??= "";
			var version = TexturePatchService.CreateCacheKey(bundleName, assetName, "").version;

			if (
				initialized && this.bundleName == bundleName && this.assetName == assetName &&
				bundleVersion.Equals(version)
			) {
				DiscoverAndStartSources(retryFailed: true);
				return;
			}

			ReleaseOwnedState();

			this.bundleName = bundleName;
			this.assetName = assetName;
			bundleVersion = version;
			initialized = true;
			disposed = false;

			DiscoverAndStartSources(retryFailed: false);
		}

		private void DiscoverAndStartSources(bool retryFailed) {
			discovering = true;
			try {
				DiscoverRenderers();
				foreach (var renderer in renderers.Values) {
					if (!renderer.skeletonRenderer) continue;

					DiscoverAtlasMaterials(renderer);
					DiscoverCurrentMaterials(renderer, force: true);
					DiscoverSeparatorMaterials(renderer, force: true);
				}
			} finally {
				discovering = false;
			}

			foreach (var work in sources.Values)
				StartSourceWork(work, retryFailed);
		}

		private void DiscoverRenderers() {
			var skeletonRenderers = GetComponentsInChildren<SkeletonRenderer>(true);
			for (var rendererIndex = 0; rendererIndex < skeletonRenderers.Length; rendererIndex++) {
				var skeletonRenderer = skeletonRenderers[rendererIndex];
				if (!skeletonRenderer) continue;

				var instanceId = skeletonRenderer.GetInstanceID();
				if (renderers.ContainsKey(instanceId)) continue;

				var state = new RendererState(rendererIndex, skeletonRenderer);
				if (!state.meshRenderer || state.overrides == null) continue;

				renderers.Add(instanceId, state);
				skeletonRenderer.OnMeshAndMaterialsUpdated += OnMeshAndMaterialsUpdated;
				if (state.separator)
					state.separator.OnMeshAndMaterialsUpdated += OnSeparatedMeshAndMaterialsUpdated;
			}
		}

		private void DiscoverAtlasMaterials(RendererState renderer) {
			if (!renderer.skeletonRenderer) return;

			var skeletonData = renderer.skeletonRenderer.SkeletonDataAsset;
			var atlasAssets = skeletonData ? skeletonData.atlasAssets : null;
			if (atlasAssets == null) return;

			for (var atlasIndex = 0; atlasIndex < atlasAssets.Length; atlasIndex++) {
				var atlasAsset = atlasAssets[atlasIndex];
				if (!atlasAsset) continue;

				var materialIndex = 0;
				foreach (var material in atlasAsset.Materials) {
					RegisterMaterial(renderer, material, $"S{renderer.index:X8}:A{atlasIndex:X8}:M{materialIndex:X8}");
					materialIndex++;
				}
			}
		}

		private void OnMeshAndMaterialsUpdated(SkeletonRenderer skeletonRenderer) {
			if (
				disposed || !skeletonRenderer ||
				!renderers.TryGetValue(skeletonRenderer.GetInstanceID(), out var renderer)
			) return;

			DiscoverCurrentMaterials(renderer, force: false);
		}

		private void OnSeparatedMeshAndMaterialsUpdated(SkeletonRenderer skeletonRenderer) {
			if (
				disposed || !skeletonRenderer ||
				!renderers.TryGetValue(skeletonRenderer.GetInstanceID(), out var renderer)
			) return;

			DiscoverSeparatorMaterials(renderer, force: false);
		}

		private void DiscoverSeparatorMaterials(RendererState renderer, bool force) {
			if (!renderer.separator) return;

			ReconcileBindings(renderer);
			renderer.separatorSignatureBuffer.Clear();
			foreach (var part in renderer.separator.partsRenderers) {
				var meshRenderer = part ? part.MeshRenderer : null;
				renderer.separatorSignatureBuffer.Add(meshRenderer ? meshRenderer.GetInstanceID() : 0);

				if (!meshRenderer) {
					renderer.separatorSignatureBuffer.Add(0);
					continue;
				}

				renderer.materialBuffer.Clear();
				meshRenderer.GetSharedMaterials(renderer.materialBuffer);
				renderer.separatorSignatureBuffer.Add(renderer.materialBuffer.Count);
				foreach (var material in renderer.materialBuffer) {
					renderer.separatorSignatureBuffer.Add(material ? material.GetInstanceID() : 0);

					var texture = material ? material.mainTexture as Texture2D : null;
					renderer.separatorSignatureBuffer.Add(texture ? texture.GetInstanceID() : 0);
				}
			}

			var signatureChanged = force ||
				renderer.separatorSignature.Count != renderer.separatorSignatureBuffer.Count;
			if (!signatureChanged) {
				for (var i = 0; i < renderer.separatorSignature.Count; i++) {
					if (renderer.separatorSignature[i] == renderer.separatorSignatureBuffer[i])
						continue;

					signatureChanged = true;
					break;
				}
			}
			if (!signatureChanged) return;

			renderer.separatorSignature.Clear();
			renderer.separatorSignature.AddRange(renderer.separatorSignatureBuffer);
			for (var partIndex = 0; partIndex < renderer.separator.partsRenderers.Count; partIndex++) {
				var part = renderer.separator.partsRenderers[partIndex];
				var meshRenderer = part ? part.MeshRenderer : null;
				if (!meshRenderer) continue;

				renderer.materialBuffer.Clear();
				meshRenderer.GetSharedMaterials(renderer.materialBuffer);
				for (var materialIndex = 0; materialIndex < renderer.materialBuffer.Count; materialIndex++) {
					var material = renderer.materialBuffer[materialIndex];
					if (!material || ownedMaterialIds.Contains(material.GetInstanceID())) continue;

					var original = FindOverrideSource(renderer, material) ?? material;
					RegisterMaterial(renderer, original, CreateRuntimeDiscriminator(renderer, material, $"P{partIndex:X8}:M{materialIndex:X8}"));
				}
			}
		}

		private void DiscoverCurrentMaterials(RendererState renderer, bool force) {
			if (!renderer.meshRenderer) return;

			ReconcileBindings(renderer);

			renderer.materialBuffer.Clear();
			renderer.meshRenderer.GetSharedMaterials(renderer.materialBuffer);

			var signatureChanged = force ||
				renderer.materialSignature.Count != renderer.materialBuffer.Count * 2;
			if (!signatureChanged) {
				for (var i = 0; i < renderer.materialBuffer.Count; i++) {
					var material = renderer.materialBuffer[i];
					var signatureIndex = i * 2;
					var materialId = material ? material.GetInstanceID() : 0;
					var texture = material ? material.mainTexture as Texture2D : null;
					var textureId = texture ? texture.GetInstanceID() : 0;
					if (
						renderer.materialSignature[signatureIndex] == materialId &&
						renderer.materialSignature[signatureIndex + 1] == textureId
					) continue;

					signatureChanged = true;
					break;
				}
			}
			if (!signatureChanged) return;

			renderer.materialSignature.Clear();
			for (var materialIndex = 0; materialIndex < renderer.materialBuffer.Count; materialIndex++) {
				var material = renderer.materialBuffer[materialIndex];
				renderer.materialSignature.Add(material ? material.GetInstanceID() : 0);
				var texture = material ? material.mainTexture as Texture2D : null;
				renderer.materialSignature.Add(texture ? texture.GetInstanceID() : 0);
				if (!material || ownedMaterialIds.Contains(material.GetInstanceID())) continue;

				var original = FindOverrideSource(renderer, material) ?? material;
				RegisterMaterial(renderer, original, CreateRuntimeDiscriminator(renderer, material, $"M{materialIndex:X8}"));
			}
		}

		private void ReconcileBindings(RendererState renderer) {
			ReleaseRetiredMaterials(renderer);
			renderer.staleBindings.Clear();

			foreach (var binding in renderer.bindings.Values) {
				var mapped = binding.clone &&
					renderer.overrides.TryGetValue(binding.original, out var current) &&
					current == binding.clone;
				var template = binding.hadPreviousOverride && binding.previousOverride
					? binding.previousOverride
					: binding.original;
				var sourceMatches = template && template.mainTexture == binding.source.source;

				if (!mapped || !sourceMatches) {
					renderer.staleBindings.Add(binding.original);
					continue;
				}

				if (
					binding.source.status == SourceStatus.Ready &&
					binding.source.replacement &&
					binding.clone.mainTexture != binding.source.replacement
				)
					binding.clone.mainTexture = binding.source.replacement;
			}

			foreach (var original in renderer.staleBindings) {
				if (!renderer.bindings.TryGetValue(original, out var binding)) continue;

				var restorePreviousOverride = binding.clone &&
					renderer.overrides.TryGetValue(original, out var current) &&
					current == binding.clone;
				var discriminator = binding.discriminator;
				UnregisterMaterial(binding, restorePreviousOverride);
				RegisterMaterial(renderer, original, discriminator);
			}
			renderer.staleBindings.Clear();
		}

		private void ReleaseRetiredMaterials(RendererState renderer) {
			foreach (var material in renderer.retiredMaterials) {
				if (!material) continue;

				ownedMaterialIds.Remove(material.GetInstanceID());
				ownedMaterials.Remove(material);
				UnityEngine.Object.Destroy(material);
			}
			renderer.retiredMaterials.Clear();
		}

		private Material FindOverrideSource(RendererState renderer, Material current) {
			foreach (var binding in renderer.bindings.Values)
				if (binding.clone == current || binding.previousOverride == current)
					return binding.original;

			foreach (var pair in renderer.overrides)
				if (pair.Value == current) return pair.Key;

			return null;
		}

		private static string CreateRuntimeDiscriminator(RendererState renderer, Material material, string consumer) {
			var materialName = material.name ?? "";
			var texture = material.mainTexture as Texture2D;
			var textureName = texture ? texture.name ?? "" : "";
			return string.Concat(
				"S", renderer.index.ToString("X8"),
				":R:", consumer,
				":", materialName.Length.ToString("X8"), ":", materialName,
				":", textureName.Length.ToString("X8"), ":", textureName,
				":", (texture ? texture.width : 0).ToString("X8"),
				":", (texture ? texture.height : 0).ToString("X8")
			);
		}

		private void RegisterMaterial(
			RendererState renderer,
			Material original,
			string discriminator
		) {
			if (!original) return;
			if (renderer.bindings.TryGetValue(original, out var existing)) {
				existing.source.RegisterDiscriminator(discriminator);
				return;
			}

			var hadPreviousOverride = renderer.overrides.TryGetValue(
				original,
				out var previousOverride
			);
			var template = previousOverride ? previousOverride : original;
			if (!(template.mainTexture is Texture2D sourceTexture)) return;
			if (!TexturePatchService.CanPatch(sourceTexture)) return;

			var work = GetOrCreateSource(sourceTexture, discriminator);
			Material clone = null;
			MaterialBinding binding = null;
			var overrideInstalled = false;
			var bindingAdded = false;
			var workBindingAdded = false;
			var ownedMaterialAdded = false;
			var ownedMaterialIdAdded = false;
			var registered = false;
			try {
				clone = new Material(template) {
					name = (template.name ?? "") + TexturePatcherBase.TextureFilterName,
					hideFlags = template.hideFlags
				};
				clone.mainTexture = work.status == SourceStatus.Ready
					? work.replacement
					: sourceTexture;

				binding = new MaterialBinding(
					renderer,
					original,
					previousOverride,
					hadPreviousOverride,
					clone,
					work,
					discriminator
				);

				renderer.overrides[original] = clone;
				overrideInstalled = true;
				renderer.bindings.Add(original, binding);
				bindingAdded = true;
				work.bindings.Add(binding);
				workBindingAdded = true;
				ownedMaterials.Add(clone);
				ownedMaterialAdded = true;
				ownedMaterialIdAdded = ownedMaterialIds.Add(clone.GetInstanceID());
				registered = true;
			} catch (Exception exception) {
				if (
					overrideInstalled &&
					renderer.overrides.TryGetValue(original, out var current) &&
					current == clone
				) {
					if (hadPreviousOverride)
						renderer.overrides[original] = previousOverride;
					else
						renderer.overrides.Remove(original);
				}

				if (bindingAdded) renderer.bindings.Remove(original);
				if (workBindingAdded) work.bindings.Remove(binding);
				if (ownedMaterialAdded) ownedMaterials.Remove(clone);
				if (ownedMaterialIdAdded) ownedMaterialIds.Remove(clone.GetInstanceID());
				if (clone) UnityEngine.Object.Destroy(clone);

				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to register Spine material override on '{name}': {exception}");
			}

			if (registered && !discovering)
				StartSourceWork(work, retryFailed: true);
		}

		private void UnregisterMaterial(
			MaterialBinding binding,
			bool restorePreviousOverride
		) {
			if (restorePreviousOverride) {
				if (binding.hadPreviousOverride)
					binding.renderer.overrides[binding.original] = binding.previousOverride;
				else
					binding.renderer.overrides.Remove(binding.original);
			}

			binding.renderer.bindings.Remove(binding.original);
			binding.source.bindings.Remove(binding);
			if (binding.clone && !binding.renderer.retiredMaterials.Contains(binding.clone))
				binding.renderer.retiredMaterials.Add(binding.clone);
		}

		private SourceWork GetOrCreateSource(Texture2D source, string discriminator) {
			var instanceId = source.GetInstanceID();
			if (
				sources.TryGetValue(instanceId, out var work) &&
				ReferenceEquals(work.source, source)
			) {
				work.RegisterDiscriminator(discriminator);
				return work;
			}

			work = new SourceWork(source, discriminator);
			sources[instanceId] = work;
			return work;
		}

		private void StartSourceWork(SourceWork work, bool retryFailed) {
			if (retryFailed && work.status == SourceStatus.Failed)
				work.status = SourceStatus.Waiting;

			if (
				!initialized || disposed ||
				work.status != SourceStatus.Waiting || work.bindings.Count == 0 ||
				!work.source
			) return;

			try {
				work.status = SourceStatus.Pending;
				var runner = coroutineRunner ? coroutineRunner : this;
				runner.StartCoroutine(CreateSourceTexture(work));
			} catch (Exception exception) {
				work.status = SourceStatus.Failed;
				Plugin.Logger.LogWarning(
					$"[Symphony::Experimental] Failed to start Spine texture patch on '{name}': {exception}"
				);
			}
		}

		private IEnumerator CreateSourceTexture(SourceWork work) {
			var completed = false;
			try {
				var textureName = work.source ? work.source.name ?? "" : "";
				var cacheName = string.Concat(
					textureName.Length.ToString("X8"), ":", textureName,
					":SP1:", work.discriminator ?? "",
					":", work.source.width.ToString("X8"),
					":", work.source.height.ToString("X8")
				);
				var cacheKey = new TextureCacheKey(bundleName, assetName, bundleVersion, cacheName);

				yield return TexturePatchService.CreateTextureAsync(
					cacheKey,
					work.source,
					texture => {
						completed = true;
						CompleteSourceWork(work, texture);
					},
					() => {
						completed = true;
						if (work.status == SourceStatus.Pending)
							work.status = SourceStatus.Failed;
					},
					pma: 0
				);
			} finally {
				if (!completed && work.status == SourceStatus.Pending)
					work.status = SourceStatus.Failed;
			}
		}

		private void CompleteSourceWork(SourceWork work, Texture2D texture) {
			var isCurrent = sources.TryGetValue(work.instanceId, out var current) &&
				ReferenceEquals(current, work);
			if (
				disposed || !texture ||
				!isCurrent || work.bindings.Count == 0
			) {
				if (texture) UnityEngine.Object.Destroy(texture);
				if (!disposed && isCurrent)
					work.status = SourceStatus.Failed;
				return;
			}

			work.replacement = texture;
			work.status = SourceStatus.Ready;
			ownedTextures.Add(texture);

			foreach (var binding in work.bindings) {
				try {
					if (binding.clone) binding.clone.mainTexture = texture;
				} catch (Exception exception) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to apply Spine texture '{work.source?.name}' on '{name}': {exception}");
				}
			}
		}

		private void OnEnable() {
			if (!initialized || disposed) return;

			foreach (var work in sources.Values)
				StartSourceWork(work, retryFailed: true);
		}

		private void ReleaseOwnedState() {
			if (!coroutineRunner || coroutineRunner == this)
				StopAllCoroutines();

			foreach (var renderer in renderers.Values) {
				if (renderer.skeletonRenderer)
					renderer.skeletonRenderer.OnMeshAndMaterialsUpdated -= OnMeshAndMaterialsUpdated;

				if (renderer.separator)
					renderer.separator.OnMeshAndMaterialsUpdated -= OnSeparatedMeshAndMaterialsUpdated;

				foreach (var binding in renderer.bindings.Values) {
					try {
						if (
							renderer.overrides.TryGetValue(binding.original, out var current) &&
							current == binding.clone
						) {
							if (binding.hadPreviousOverride)
								renderer.overrides[binding.original] = binding.previousOverride;
							else
								renderer.overrides.Remove(binding.original);
						}
					} catch {
						// Spine may already be disposing its managed state.
					}
				}
			}

			foreach (var material in ownedMaterials)
				if (material) UnityEngine.Object.Destroy(material);

			foreach (var texture in ownedTextures)
				if (texture) UnityEngine.Object.Destroy(texture);

			renderers.Clear();
			sources.Clear();
			ownedMaterialIds.Clear();
			ownedMaterials.Clear();
			ownedTextures.Clear();
		}

		private void OnDestroy() {
			disposed = true;
			initialized = false;
			ReleaseOwnedState();
		}
	}
}
