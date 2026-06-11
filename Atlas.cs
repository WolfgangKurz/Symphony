using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEngine;

namespace Symphony {
	internal class Atlas {
		private const int DefaultAtlasPadding = 2;
		private const int DefaultMaxAtlasSize = 2048;

		public static NGUIAtlas atlas { get; private set; }

		static Atlas() {
			Setup_Atlas(new Dictionary<string, byte[]>() {
				{ "UI_SelectWorldBtn_MainStory_Small", Resource.UI_SelectWorldBtn_MainStory_Small },
				{ "UI_SelectWorldBtn_MainStory_Small_Half", Resource.UI_SelectWorldBtn_MainStory_Small_Half },
				{ "UI_Import", Resource.UI_Import },
				{ "UI_Export", Resource.UI_Export },
			});
		}
		private static void Setup_Atlas(
			Dictionary<string, byte[]> images,
			string atlasName = "SymphonyAtlas",
			int padding = DefaultAtlasPadding,
			int maxAtlasSize = DefaultMaxAtlasSize
		) {
			if (atlas != null) return;
			if (images == null) throw new ArgumentNullException(nameof(images));
			if (images.Count == 0) throw new ArgumentException("Atlas images must not be empty.", nameof(images));

			var src_mat = GetSourceMaterial();
			var sources = new List<PackedSource>(images.Count);

			try {
				foreach (var image in images)
					sources.Add(new PackedSource(image.Key, LoadTexture(image.Key, image.Value)));

				var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true) {
					name = atlasName,
					wrapMode = TextureWrapMode.Clamp,
				};
				var rects = tex.PackTextures(sources.Select(x => x.texture).ToArray(), padding, maxAtlasSize, false);
				if (rects == null || rects.Length != sources.Count) {
					throw new InvalidOperationException("Unity failed to pack atlas textures.");
				}

				var sprites = new List<UISpriteData>(sources.Count);
				for (var i = 0; i < sources.Count; i++) {
					var source = sources[i];
					var rect = rects[i];
					var width = Mathf.RoundToInt(rect.width * tex.width);
					var height = Mathf.RoundToInt(rect.height * tex.height);
					if (width != source.texture.width || height != source.texture.height) {
						throw new InvalidOperationException($"Atlas '{atlasName}' exceeded {maxAtlasSize}px and Unity scaled '{source.name}' from {source.texture.width}x{source.texture.height} to {width}x{height}.");
					}

					var x = Mathf.RoundToInt(rect.x * tex.width);
					var y = tex.height - Mathf.RoundToInt(rect.y * tex.height) - height;
					sprites.Add(new UISpriteData {
						name = source.name,
						x = x,
						y = y,
						width = width,
						height = height,
						paddingLeft = 0,
						paddingTop = 0,
						paddingRight = 0,
						paddingBottom = 0,
						borderLeft = 0,
						borderTop = 0,
						borderRight = 0,
						borderBottom = 0,
					});
				}

				Atlas.atlas = CreateAtlas(src_mat, tex, atlasName, sprites);
			} finally {
				foreach (var source in sources)
					UnityEngine.Object.Destroy(source.texture);
			}
		}

		private static Texture2D LoadTexture(string name, byte[] bytes) {
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("Atlas sprite name must not be empty.", nameof(name));
			if (bytes == null || bytes.Length == 0) throw new ArgumentException($"Atlas sprite '{name}' has no image data.", nameof(bytes));

			var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true) {
				name = name,
				wrapMode = TextureWrapMode.Clamp,
			};
			if (!tex.LoadImage(bytes)) {
				UnityEngine.Object.Destroy(tex);
				throw new ArgumentException($"Atlas sprite '{name}' image data could not be decoded.", nameof(bytes));
			}

			return tex;
		}

		private static Material GetSourceMaterial() {
			var src_sprite = GameObject.FindObjectsByType<UISprite>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.FirstOrDefault(x => x.atlas != null);

			if (src_sprite == null)
				throw new InvalidOperationException("Cannot create Symphony atlas because no source UISprite atlas was found.");

			return (Material)src_sprite.atlas.GetType()
				.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(src_sprite.atlas);
		}

		private static NGUIAtlas CreateAtlas(Material src_mat, Texture2D tex, string atlasName, List<UISpriteData> sprites) {
			var target = ScriptableObject.CreateInstance<NGUIAtlas>();
			var mat = new Material(src_mat) {
				name = atlasName,
				mainTexture = tex,
			};

			var t = target.GetType();
			t.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, mat);
			t.GetField("materialBright", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, mat);
			t.GetField("materialCustom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, mat);
			t.GetField("materialGray", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, mat);
			t.GetField("mSpriteIndices", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(
				target,
				sprites.Select((sprite, i) => new { sprite.name, i }).ToDictionary(x => x.name, x => x.i)
			);
			t.GetField("mSprites", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, sprites);

			return target;
		}

		private class PackedSource {
			public string name { get; }
			public Texture2D texture { get; }

			public PackedSource(string name, Texture2D texture) {
				this.name = name;
				this.texture = texture;
			}
		}
	}
}
