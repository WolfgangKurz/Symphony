public struct UNIT_STAT {
	public int ATK { get; set; }
	public int DEF { get; set; }
	public int HP { get; set; }
	public int ACC { get; set; }
	public int EVA { get; set; }
	public int CRI { get; set; }
	public int SPD { get; set; }
	public int Res_Fire { get; set; }
	public int Res_Frost { get; set; }
	public int Res_Elec { get; set; }
}

public enum TARGET_TYPE {
	SELF,
	OUR,
	OUR_GRID,
	ENEMY,
	ENEMY_GRID,
	ALL_UNIT,
	ALL_GRID,
	SYSTEM,
	OUR_ALL,
	ENEMY_ALL,
}

public enum ACTOR_BODY_TYPE {
	ANDROID,
	ROBOT,
	SUMMON,
	TOTEM,
}
internal static class TypesHelper {
	public static bool IsForTeam(this TARGET_TYPE v)
		=> v == TARGET_TYPE.OUR || v == TARGET_TYPE.OUR_GRID || v == TARGET_TYPE.OUR_ALL || v == TARGET_TYPE.SELF;
	public static bool IsForEnemy(this TARGET_TYPE v) => !v.IsForTeam();

	public static bool IsForGrid(this TARGET_TYPE v)
		=> v == TARGET_TYPE.ENEMY_GRID || v == TARGET_TYPE.OUR_GRID || v == TARGET_TYPE.ALL_GRID;
	public static bool IsForSpecificTarget(this TARGET_TYPE v) => !v.IsForGrid();
}
