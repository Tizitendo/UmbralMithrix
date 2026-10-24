using RoR2;
using EntityStates;

namespace UmbralMithrix.EntityStates;

public class EnterUmbralUlt : BaseState
{
  public static string soundString = "Play_moonBrother_blueWall_slam_start";
  public static float duration = 0.2f;

  public override void OnEnter()
  {
    base.OnEnter();
    this.PlayCrossfade("Body", "UltEnter", "Ult.playbackRate", EnterUmbralUlt.duration, 0.1f);
    Util.PlaySound(EnterUmbralUlt.soundString, this.gameObject);
  }

  public override void FixedUpdate()
  {
    base.FixedUpdate();
    if (!this.isAuthority || (double)this.fixedAge <= EnterUmbralUlt.duration)
      return;
    this.outer.SetNextState(new ChannelUmbralUlt());
  }
}
