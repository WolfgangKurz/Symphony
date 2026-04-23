using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Symphony {
	internal class Atlas {
		public static NGUIAtlas atlas { get; private set; }
		public static void Setup_Atlas() {
			if (atlas == null) {
				atlas = ScriptableObject.CreateInstance<NGUIAtlas>();

				var src_sprite = GameObject.FindObjectsByType<UISprite>(FindObjectsInactive.Include, FindObjectsSortMode.None)
					.FirstOrDefault(x => x.atlas != null);
				var src_atlas = src_sprite.atlas;
				var src_mat = src_atlas.XGetFieldValue<Material>("material");

				var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
				tex.LoadImage(Resource.SymphonyAtlas);

				var mat = new Material(src_mat);
				mat.name = "SymphonyAtlas";
				mat.mainTexture = tex;

				AtlasHelper.SetAtlasMaterialFields(atlas, mat);
				atlas.XSetFieldValue("mSpriteIndices", new Dictionary<string, int> {
					{ "UI_SelectWorldBtn_MainStory_Small", 0 },
					{ "UI_SelectWorldBtn_MainStory_Small_Half", 1 }
				});
				atlas.XSetFieldValue("mSprites", new List<UISpriteData> {
					new UISpriteData {
						name = "UI_SelectWorldBtn_MainStory_Small",
						x = 0,
						y = 0,
						width = 644,
						height = 280,
						paddingLeft = 0,
						paddingTop = 3,
						paddingRight = 28,
						paddingBottom = 24,
						borderLeft = 0,
						borderTop = 0,
						borderRight = 0,
						borderBottom = 0,
					},
					new UISpriteData {
						name = "UI_SelectWorldBtn_MainStory_Small_Half",
						x = 0,
						y = 280,
						width = 322,
						height = 280,
						paddingLeft = 0,
						paddingTop = 3,
						paddingRight = 28,
						paddingBottom = 24,
						borderLeft = 0,
						borderTop = 0,
						borderRight = 0,
						borderBottom = 0,
					},
				});
			}
		}

		internal static NGUIAtlas MergeAtlases(
			INGUIAtlas primary,
			INGUIAtlas secondary,
			string mergedName = null,
			bool keepPrimaryOnConflict = true
		) => MergeAtlases(primary, secondary, null, mergedName, keepPrimaryOnConflict);

		internal static NGUIAtlas MergeAtlases(
			INGUIAtlas primary,
			INGUIAtlas secondary,
			IEnumerable<string> secondarySpriteKeys,
			string mergedName = null,
			bool keepPrimaryOnConflict = true
		) {
			if (primary == null && secondary == null) return null;
			if (primary == null) return secondary.CloneAtlas(secondarySpriteKeys, mergedName);
			if (secondary == null) return primary.CloneAtlas(mergedName);

			var primaryTexture = AtlasHelper.ReadTexture(AtlasHelper.GetAtlasTexture(primary));
			var secondaryTexture = AtlasHelper.ReadTexture(AtlasHelper.GetAtlasTexture(secondary));
			if (primaryTexture == null || secondaryTexture == null)
				return null;

			var secondaryRegion = AtlasHelper.GetHorizontalSpriteRegion(
				secondary,
				secondaryTexture.width,
				secondarySpriteKeys
			);
			var mergedTexture = new Texture2D(
				primaryTexture.width + secondaryRegion.width,
				Mathf.Max(primaryTexture.height, secondaryTexture.height),
				TextureFormat.ARGB32,
				false,
				true
			) {
				name = mergedName ?? $"{AtlasHelper.GetAtlasName(primary)}+{AtlasHelper.GetAtlasName(secondary)}"
			};

			var clearPixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), mergedTexture.width * mergedTexture.height).ToArray();
			mergedTexture.SetPixels32(clearPixels);
			mergedTexture.SetPixels32(0, 0, primaryTexture.width, primaryTexture.height, primaryTexture.GetPixels32());
			if (secondaryRegion.width > 0) {
				mergedTexture.SetPixels(
					primaryTexture.width,
					0,
					secondaryRegion.width,
					secondaryTexture.height,
					secondaryTexture.GetPixels(secondaryRegion.x, 0, secondaryRegion.width, secondaryTexture.height)
				);
			}
			mergedTexture.Apply(false, false);

			var mergedMaterial = AtlasHelper.BuildMergedMaterial(primary, mergedTexture);
			var mergedAtlas = ScriptableObject.CreateInstance<NGUIAtlas>();
			mergedAtlas.name = mergedTexture.name;
			AtlasHelper.SetAtlasMaterialFields(mergedAtlas, mergedMaterial);

			var spriteIndices = new Dictionary<string, int>(StringComparer.Ordinal);
			var sprites = new List<UISpriteData>();
			AtlasHelper.AppendAtlasSprites(primary, 0, spriteIndices, sprites, keepExisting: true);
			AtlasHelper.AppendAtlasSprites(
				secondary,
				primaryTexture.width - secondaryRegion.x,
				spriteIndices,
				sprites,
				keepPrimaryOnConflict,
				secondarySpriteKeys
			);

			mergedAtlas.XSetFieldValue("mSpriteIndices", spriteIndices);
			mergedAtlas.XSetFieldValue("mSprites", sprites);
			return mergedAtlas;
		}
	}

	file static class AtlasHelper {
		public static void AppendAtlasSprites(
			INGUIAtlas source,
			int xOffset,
			Dictionary<string, int> spriteIndices,
			List<UISpriteData> sprites,
			bool keepExisting,
			IEnumerable<string> includedSpriteKeys = null
		) {
			if (source == null)
				return;

			HashSet<string> included = null;
			if (includedSpriteKeys != null)
				included = new HashSet<string>(includedSpriteKeys, StringComparer.Ordinal);

			var names = source.GetListOfSprites();
			if (names == null)
				return;

			for (var i = 0; i < names.size; i++) {
				var spriteName = names.buffer[i];
				if (string.IsNullOrEmpty(spriteName))
					continue;
				if (included != null && !included.Contains(spriteName))
					continue;

				var sourceSprite = source.GetSprite(spriteName);
				if (sourceSprite == null)
					continue;

				var clonedSprite = sourceSprite.Clone();
				clonedSprite.x += xOffset;

				if (spriteIndices.TryGetValue(spriteName, out var existingIndex)) {
					if (keepExisting)
						continue;

					sprites[existingIndex] = clonedSprite;
					continue;
				}

				spriteIndices[spriteName] = sprites.Count;
				sprites.Add(clonedSprite);
			}
		}

		public static Material BuildMergedMaterial(INGUIAtlas atlasSource, Texture2D texture) {
			var sourceMaterial = GetAtlasMaterial(atlasSource);
			Material material;
			if (sourceMaterial != null)
				material = new Material(sourceMaterial);
			else
				material = new Material(Shader.Find("Unlit/Transparent Colored"));

			material.name = texture.name;
			material.mainTexture = texture;
			return material;
		}

		public static void SetAtlasMaterialFields(NGUIAtlas target, Material material) {
			target.XSetFieldValue("material", material);
			target.XSetFieldValue("materialBright", material);
			target.XSetFieldValue("materialCustom", material);
			target.XSetFieldValue("materialGray", material);
		}

		public static Material GetAtlasMaterial(INGUIAtlas atlas)
			=> atlas?.XGetFieldValue<Material>("material") ?? atlas?.XGetPropertyValue<Material>("material");

		public static Texture GetAtlasTexture(INGUIAtlas atlas)
			=> GetAtlasMaterial(atlas)?.mainTexture;

		public static Texture2D ReadTexture(Texture source) {
			if (source == null)
				return null;

			var sourceTexture = source as Texture2D;
			if (sourceTexture == null) {
				return null;
			}

			var previous = RenderTexture.active;
			var rt = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(sourceTexture, rt);
			RenderTexture.active = rt;

			var readable = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.ARGB32, false, true);
			readable.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
			readable.Apply(false, false);

			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(rt);
			return readable;
		}

		public static string GetAtlasName(INGUIAtlas atlas) {
			if (atlas is UnityEngine.Object unityObject && !string.IsNullOrEmpty(unityObject.name))
				return unityObject.name;
			return "MergedAtlas";
		}

		public static NGUIAtlas CloneAtlas(this INGUIAtlas source, string cloneName = null)
			=> CloneAtlas(source, null, cloneName);

		public static NGUIAtlas CloneAtlas(this INGUIAtlas source, IEnumerable<string> includedSpriteKeys, string cloneName = null) {
			if (source == null)
				return null;

			var sourceTexture = ReadTexture(GetAtlasTexture(source));
			if (sourceTexture == null)
				return null;

			var region = GetHorizontalSpriteRegion(source, sourceTexture.width, includedSpriteKeys);
			var cloneWidth = Mathf.Max(region.width, 1);
			var cloneTexture = new Texture2D(cloneWidth, sourceTexture.height, TextureFormat.ARGB32, false, true) {
				name = cloneName ?? GetAtlasName(source)
			};
			var clearPixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), cloneWidth * sourceTexture.height).ToArray();
			cloneTexture.SetPixels32(clearPixels);
			if (region.width > 0) {
				cloneTexture.SetPixels(
					0,
					0,
					region.width,
					sourceTexture.height,
					sourceTexture.GetPixels(region.x, 0, region.width, sourceTexture.height)
				);
			}
			cloneTexture.Apply(false, false);

			var cloneMaterial = BuildMergedMaterial(source, cloneTexture);
			var cloneAtlas = ScriptableObject.CreateInstance<NGUIAtlas>();
			cloneAtlas.name = cloneTexture.name;
			SetAtlasMaterialFields(cloneAtlas, cloneMaterial);

			var spriteIndices = new Dictionary<string, int>(StringComparer.Ordinal);
			var sprites = new List<UISpriteData>();
			AppendAtlasSprites(source, -region.x, spriteIndices, sprites, keepExisting: true, includedSpriteKeys);

			cloneAtlas.XSetFieldValue("mSpriteIndices", spriteIndices);
			cloneAtlas.XSetFieldValue("mSprites", sprites);
			return cloneAtlas;
		}

		public static RectInt GetHorizontalSpriteRegion(
			INGUIAtlas atlas,
			int defaultWidth,
			IEnumerable<string> includedSpriteKeys = null
		) {
			if (atlas == null)
				return new RectInt(0, 0, 0, 0);

			if (includedSpriteKeys == null)
				return new RectInt(0, 0, defaultWidth, 0);

			var included = new HashSet<string>(includedSpriteKeys, StringComparer.Ordinal);
			if (included.Count == 0)
				return new RectInt(0, 0, 0, 0);

			var minX = int.MaxValue;
			var maxX = int.MinValue;
			foreach (var spriteName in included) {
				var sprite = atlas.GetSprite(spriteName);
				if (sprite == null)
					continue;

				minX = Mathf.Min(minX, sprite.x);
				maxX = Mathf.Max(maxX, sprite.x + sprite.width);
			}

			if (minX == int.MaxValue || maxX <= minX)
				return new RectInt(0, 0, 0, 0);

			return new RectInt(minX, 0, maxX - minX, 0);
		}


		public static UISpriteData Clone(this UISpriteData source) {
			return new UISpriteData {
				name = source.name,
				x = source.x,
				y = source.y,
				width = source.width,
				height = source.height,
				borderLeft = source.borderLeft,
				borderRight = source.borderRight,
				borderTop = source.borderTop,
				borderBottom = source.borderBottom,
				paddingLeft = source.paddingLeft,
				paddingRight = source.paddingRight,
				paddingTop = source.paddingTop,
				paddingBottom = source.paddingBottom,
			};
		}
	}
}
