using System.Collections.Generic;
using UnityEngine;
using VLB;
using static SamSite;

namespace Oxide.Plugins
{

	[Info("Heli Sams", "Whispers88 & Whitethunder", "1.1.4")]
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

		private class SAMTargetComponent : FacepunchBehaviour, ISamSiteTarget
		{
			public static List<SAMTargetComponent> SAMTargetComponents= new List<SAMTargetComponent>();
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
				SAMTargetComponents.Add(this);
				baseEntity = GetComponent<BaseEntity>();
				if (baseEntity is BaseHelicopter)
				{
					_child = baseEntity.gameObject.CreateChild();
					_child.gameObject.layer = (int)Rust.Layer.Vehicle_World;
					_child.AddComponent<SphereCollider>();
				}
			}
			public BaseEntity baseEntity;

			private void OnDestroy()
			{
				if (_child != null)
					DestroyImmediate(_child);
				if (SAMTargetComponents.Contains(this))
					SAMTargetComponents.Remove(this);

			}
			public bool IsValidSAMTarget() => true;

			public Vector3 Position => baseEntity.transform.position;

			public SamTargetType SAMTargetType => SamSite.targetTypeVehicle;

			public bool isClient => false;

			public bool IsValidSAMTarget(bool isStaticSamSite) => true;

			public Vector3 CenterPoint() => baseEntity.CenterPoint();

			public Vector3 GetWorldVelocity() => baseEntity.GetWorldVelocity();
			public bool IsVisible(Vector3 position, float distance) => baseEntity.IsVisible(position, distance);

		}
		private void OnSamSiteTargetScan(SamSite samSite, List<ISamSiteTarget> targetList)
		{
			if (SAMTargetComponent.SAMTargetComponents.Count == 0)
				return;

			foreach (var SAMtargetcomp in SAMTargetComponent.SAMTargetComponents)
			{
				targetList.Add(SAMtargetcomp);
			}
		}

        private void CanSamSiteShoot(SamSite samSite)
        {
			BaseEntity target = null;
			foreach (var SAMtargetcomp in SAMTargetComponent.SAMTargetComponents)
			{
				if(target == null || Vector3.Distance(SAMtargetcomp.baseEntity.transform.position, samSite.transform.position) > 50f || Vector3.Distance(SAMtargetcomp.baseEntity.transform.position, samSite.transform.position) < Vector3.Distance(target.transform.position, samSite.transform.position))
				target = SAMtargetcomp.baseEntity;
			}

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

        private Vector3 PredictedPos(BaseEntity target, SamSite samSite, Vector3 targetVelocity)
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