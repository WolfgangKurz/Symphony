using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEngine;

namespace Symphony {
	internal class Atlas {
		public static NGUIAtlas atlas { get; private set; }
		public static void Setup_Atlas() {
			if (atlas == null) {
				atlas = ScriptableObject.CreateInstance<NGUIAtlas>();

				var src_sprite = GameObject.FindObjectsOfType<UISprite>(true).FirstOrDefault(x => x.atlas != null);
				var src_atlas = src_sprite.atlas;
				var src_mat = (Material)src_atlas.GetType()
					.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic)
					.GetValue(src_atlas);

				var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
				tex.LoadImage(Resource.SymphonyAtlas);

				var mat = new Material(src_mat);
				mat.name = "SymphonyAtlas";
				mat.mainTexture = tex;

				var t = atlas.GetType();
				t.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialBright", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialCustom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialGray", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);

				t.GetField("mSpriteIndices", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, new Dictionary<string, int> {
					{ "UI_SelectWorldBtn_MainStory_Small", 0 },
					{ "UI_SelectWorldBtn_MainStory_Small_Half", 1 }
				});
				t.GetField("mSprites", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, new List<UISpriteData> {
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
	}
}
