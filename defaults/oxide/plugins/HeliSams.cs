using UnityEngine;
using VLB;

namespace Oxide.Plugins
{

	[Info("Heli Sams", "Whispers88 & Whitethunder", "1.1.2")]
	[Description("Lets your Samsites target Patrol Helicopters")]

	public class HeliSams : RustPlugin
	{
		public static HeliSams heliSams;

		#region Hooks
		void OnEntitySpawned(CH47Helicopter entity) => SAMTargetComponent.AddComp(entity);

		void OnEntitySpawned(BaseHelicopter entity) => SAMTargetComponent.AddComp(entity);
		
		void OnEntityKill(CH47Helicopter entity) => SAMTargetComponent.RemoveComp(entity);

		void OnEntityKill(BaseHelicopter entity) => SAMTargetComponent.RemoveComp(entity);


		private void Unload()
		{
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				if (entity is CH47Helicopter)
					SAMTargetComponent.RemoveComp(entity as BaseCombatEntity);
				if (entity is BaseHelicopter)
					SAMTargetComponent.RemoveComp(entity as BaseCombatEntity);
			}
		}

		#endregion

		private class SAMTargetComponent : EntityComponent<BaseCombatEntity>, SamSite.ISamSiteTarget
		{
			public static void AddComp(BaseCombatEntity ENT) =>
				ENT.GetOrAddComponent<SAMTargetComponent>();

			public static void RemoveComp(BaseCombatEntity ENT)
			{
				SAMTargetComponent samComponent = ENT.GetComponent<SAMTargetComponent>();
				if (samComponent != null)
					DestroyImmediate(samComponent);
			}

			private GameObject _child;
			private void Awake()
			{
				if (baseEntity is BaseHelicopter)
				{
					_child = baseEntity.gameObject.CreateChild();
					_child.gameObject.layer = (int)Rust.Layer.Vehicle_World;
					_child.AddComponent<SphereCollider>();
				}
			}

			private void OnDestroy()
			{
				if (_child != null)
					DestroyImmediate(_child);
			}
			public bool IsValidSAMTarget() => true;
		}

		private void CanSamSiteShoot(SamSite samSite)
		{
			BaseCombatEntity target = samSite.currentTarget;
			if (target == null)
				return;

			if (target is BaseHelicopter)
			{
				PatrolHelicopterAI Ai = ((BaseHelicopter)target).myAI;
				Vector3 targetVelocity = (Ai.GetLastMoveDir() * Ai.GetMoveSpeed()) * 1.25f;
				Vector3 estimatedPoint = PredictedPos(target, samSite, targetVelocity);
				samSite.currentAimDir = (estimatedPoint - samSite.eyePoint.transform.position).normalized;
				return;
			}
			if (target is CH47Helicopter)
			{
				Vector3 targetVelocity = target.gameObject.GetComponent<Rigidbody>().velocity;
				Vector3 estimatedPoint = PredictedPos(target, samSite, targetVelocity);
				samSite.currentAimDir = (estimatedPoint - samSite.eyePoint.transform.position).normalized;
				return;
			}
		}

		private Vector3 PredictedPos(BaseCombatEntity target, SamSite samSite, Vector3 targetVelocity)
		{
			Vector3 targetpos = target.transform.TransformPoint(target.transform.GetBounds().center);
			Vector3 displacement = targetpos - samSite.eyePoint.transform.position;
			float projectileSpeed = samSite.projectileTest.Get().GetComponent<ServerProjectile>().speed;
			float targetMoveAngle = Vector3.Angle(-displacement, targetVelocity) * Mathf.Deg2Rad;
			if (targetVelocity.magnitude == 0 || targetVelocity.magnitude > projectileSpeed && Mathf.Sin(targetMoveAngle) / projectileSpeed > Mathf.Cos(targetMoveAngle) / targetVelocity.magnitude)
			{
				return targetpos;
			}
			float shootAngle = Mathf.Asin(Mathf.Sin(targetMoveAngle) * targetVelocity.magnitude / projectileSpeed);
			return targetpos + targetVelocity * displacement.magnitude / Mathf.Sin(Mathf.PI - targetMoveAngle - shootAngle) * Mathf.Sin(shootAngle) / targetVelocity.magnitude;
		}
	}
}