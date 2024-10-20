/// <summary>
/// A component to help deal with Props.
/// </summary>
public sealed class PropHelper : Component, Component.ICollisionListener
{
	public struct BodyInfo
	{
		public PhysicsBodyType Type { get; set; }
		public Transform Transform { get; set; }
	}

	[Property, Sync] public float Health { get; set; } = 1f;
	[Property, Sync] public Vector3 Velocity { get; set; }

	[Sync] public Prop Prop { get; set; }
	[Sync] public ModelPhysics ModelPhysics { get; set; }
	[Sync] public Rigidbody Rigidbody { get; set; }
	[Sync] public NetDictionary<int, BodyInfo> NetworkedBodies { get; set; } = new();

	private Vector3 lastPosition = Vector3.Zero;

	protected override void OnStart()
	{
		Health = Prop?.Health ?? 0f;
		Velocity = 0f;

		lastPosition = Prop?.WorldPosition ?? WorldPosition;
	}

	[Broadcast]
	public void Damage( float amount )
	{
		if ( (Prop?.Health ?? 0f) <= 0f )
			return;

		if ( IsProxy )
			return;

		Health -= amount;

		if ( Health <= 0f )
			Kill();
	}

	public void Kill()
	{
		if ( IsProxy )
			return;

		var gibs = Prop?.CreateGibs();

		foreach ( var gib in gibs )
		{
			gib.GameObject.NetworkSpawn();
			gib.Network.SetOrphanedMode( NetworkOrphaned.Host );
		}

		GameObject.DestroyImmediate();
	}

	public void AddForce( int bodyIndex, Vector3 force )
	{
		if ( IsProxy )
			return;

		var body = ModelPhysics?.PhysicsGroup?.GetBody( bodyIndex );
		if ( body.IsValid() )
		{
			body.ApplyForce( force );
		}
		else if ( bodyIndex == 0 && Rigidbody.IsValid() )
		{
			Rigidbody.Velocity += force / Rigidbody.PhysicsBody.Mass;
		}
	}

	public async void AddDamagingForce( Vector3 force, float damage )
	{
		if ( IsProxy )
			return;

		if ( ModelPhysics.IsValid() )
		{
			foreach ( var body in ModelPhysics.PhysicsGroup.Bodies )
			{
				AddForce( body.GroupIndex, force );
			}
		}
		else
		{
			AddForce( 0, force );
		}

		await GameTask.DelaySeconds( 1f / Scene.FixedUpdateFrequency + 0.05f );

		Damage( damage );
	}

	[Broadcast]
	public void BroadcastAddForce( int bodyIndex, Vector3 force )
	{
		if ( IsProxy )
			return;

		AddForce( bodyIndex, force );
	}

	[Broadcast]
	public void BroadcastAddDamagingForce( Vector3 force, float damage )
	{
		if ( IsProxy )
			return;

		AddDamagingForce( force, damage );
	}

	protected override void OnFixedUpdate()
	{
		if ( Prop.IsValid() )
		{
			Velocity = (Prop.WorldPosition - lastPosition) / Time.Delta;
			lastPosition = Prop.WorldPosition;
		}

		UpdateNetworkedBodies();
	}

	private void UpdateNetworkedBodies()
	{
		if ( !ModelPhysics.IsValid() )
		{
			ModelPhysics = Components.Get<ModelPhysics>( FindMode.EverythingInSelf );
			Rigidbody = GetComponent<Rigidbody>();

			return;
		}

		if ( !Network.IsOwner )
		{
			var rootBody = FindRootBody();

			if ( NetworkedBodies is null )
				return;

			foreach ( var (groupId, info) in NetworkedBodies )
			{
				var group = ModelPhysics.PhysicsGroup.GetBody( groupId );
				group.Transform = info.Transform;
				group.BodyType = info.Type;
			}

			if ( rootBody.IsValid() )
				rootBody.Transform = ModelPhysics.Renderer.GameObject.WorldTransform;

			return;
		}

		foreach ( var body in ModelPhysics.PhysicsGroup.Bodies )
		{
			if ( body.GroupIndex == 0 )
				continue;

			var tx = body.GetLerpedTransform( Time.Now );
			NetworkedBodies[body.GroupIndex] = new BodyInfo
			{
				Type = body.BodyType,
				Transform = tx
			};
		}
	}

	private PhysicsBody FindRootBody()
	{
		var body = ModelPhysics.PhysicsGroup.Bodies.FirstOrDefault();
		if ( body == null )
			return null;

		while ( body.Parent.IsValid() )
			body = body.Parent;

		return body;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy )
			return;

		var speed = Velocity.Length;
		var otherSpeed = collision.Other.Body.Velocity.Length;

		if ( otherSpeed > speed )
			speed = otherSpeed;

		if ( speed >= 500f )
		{
			var dmg = speed / 10f;

			Damage( dmg );

			if ( collision.Other.GameObject.Root.Components.TryGet<Player>( out var player ) )
			{
				player.TakeDamage( dmg );
			}
		}
	}
}
