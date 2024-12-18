/// <summary>
/// Holds player information like health
/// </summary>
public sealed class Player : Component, Component.IDamageable
{
	public static Player FindLocalPlayer()
	{
		return Game.ActiveScene.GetAllComponents<Player>().Where( x => !x.IsProxy ).FirstOrDefault();
	}

	[RequireComponent] public PlayerController PlayerController { get; set; }
	[RequireComponent] public PlayerInventory PlayerInventory { get; set; }

	[Property] public GameObject Body { get; set; }
	[Property] public SkinnedModelRenderer ModelRenderer { get; set; }
	[Property, Range( 0, 100 ), Sync] public float Health { get; set; } = 100;

	public Ray AimRay => new( Scene.Camera.WorldPosition, Scene.Camera.Transform.World.Forward * 10000f );

	public bool IsDead => Health <= 0;

	public Transform EyeTransform
	{
		get
		{
			var tx = new Transform( PlayerController.EyePosition );
			tx.Rotation = PlayerController.EyeAngles;
			return tx;
		}
	}

	protected override void OnStart()
	{
		ModelRenderer = Components.GetInChildrenOrSelf<SkinnedModelRenderer>();
	}

	/// <summary>
	/// Creates a ragdoll but it isn't enabled
	/// </summary>
	/// 
	[Broadcast]
	void CreateRagdoll()
	{
		var originalBody = Body.Components.Get<SkinnedModelRenderer>();

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll", "solid", "debris" );
		go.Transform.World = Transform.World;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( originalBody );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in originalBody.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( originalBody, true );

		var corpse = go.Components.Create<PlayerCorpse>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;
	}

	[Broadcast( NetPermission.OwnerOnly )]
	void CreateRagdollAndGhost()
	{
		if ( !Networking.IsHost ) return;

		var go = new GameObject( false, "Observer" );
		go.Components.Create<PlayerObserver>();
		go.NetworkSpawn( Rpc.Caller );
	}

	public void TakeDamage( float amount )
	{
		if ( IsProxy ) return;
		if ( Health <= 0 ) return;

		Health -= amount;

		IPlayerEvent.Post( x => x.OnTakeDamage( this, amount ) );

		if ( Health <= 0 )
		{
			Health = 0;
			Death();
		}
	}

	void Death()
	{
		CreateRagdoll();
		CreateRagdollAndGhost();

		IPlayerEvent.Post( x => x.OnDied( this ) );

		GameObject.Destroy();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		TakeDamage( damage.Damage );
	}
}
