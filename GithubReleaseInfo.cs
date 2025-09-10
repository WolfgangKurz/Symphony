namespace Symphony {
	internal class GithubReleaseInfo {
		public class GithubReleaseAsset {
			public string name { get; set; }
			public long size { get; set; }
			public string browser_download_url { get; set; }
		}

		public string tag_name { get; set; }
		public GithubReleaseAsset[] assets { get; set; }
	}
}
