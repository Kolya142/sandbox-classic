using Sandbox.Citizen;

public class BaseWeapon : Component
{
	[Property] public string DisplayName { get; set; } = "My Weapon";
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.HoldItem;
	[Property] public string ParentBoneName { get; set; } = "hand_R";
	[Property] public Transform BoneOffset { get; set; } = new Transform( 0 );

	[Property] public SkinnedModelRenderer WorldModel { get; set; }
	[Property] public SkinnedModelRenderer ViewModel { get; set; }
	[Property] public SkinnedModelRenderer ViewModelArms { get; set; }

	[Property] public GameObject WorldModelMuzzle { get; set; }
	[Property] public GameObject ViewModelMuzzle { get; set; }

	public GameObject Muzzle => IsProxy ? WorldModelMuzzle : ViewModelMuzzle;

	// GameObject Root => IsProxy ? Owner.ModelRenderer.GetBoneObject(ParentBone) : Scene.Camera.GameObject;

	GameObject _parentBone;

	public GameObject ParentBone
	{
		get
		{
			if ( !_parentBone.IsValid() )
			{
				_parentBone = Owner.ModelRenderer.GetBoneObject( ParentBoneName );
			}

			return _parentBone;
		}
	}

	public Player Owner { get; set; }

	// TODO: When third person is added, fix this!
	public bool UseWorldModel => IsProxy;

	protected override void OnPreRender()
	{
		if ( !Owner.IsValid() ) return;

		ViewModel.WorldTransform = Scene.Camera.WorldTransform;
		ViewModel.Transform.ClearInterpolation();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		WorldModel.RenderType = UseWorldModel ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;
		ViewModel.GameObject.Enabled = !UseWorldModel;
		ViewModel.RenderType = ModelRenderer.ShadowRenderType.Off;

		if ( ViewModelArms.IsValid() )
			ViewModelArms.RenderType = ModelRenderer.ShadowRenderType.Off;

		GameObject.NetworkInterpolation = false;

		Owner = GameObject.Components.GetInAncestorsOrSelf<Player>();
		if ( !Owner.IsValid() )
			return;

		var body = Owner.Body.Components.Get<SkinnedModelRenderer>();
		body.Set( "holdtype", (int)HoldType );

		// TR: From what I see original Sandbox has a sine wave bob
		// AS: Yeah it does.
		var obj = body.GetBoneObject( ParentBoneName );
		if ( obj.IsValid() )
		{
			GameObject.Parent = obj;
			GameObject.LocalTransform = BoneOffset.WithScale( 1f );
		}

		OnControl();
	}

	public virtual void Spawn()
	{
	}

	public virtual void OnControl()
	{
	}

	public virtual void DoEnabled()
	{
	}
}
