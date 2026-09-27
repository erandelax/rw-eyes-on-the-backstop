using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    public static class BulletTrajectoryUtility
    {
        public static bool IsFullObstacleAtCell(Map map, IntVec3 destination)
        {
            if (map == null || !destination.InBounds(map))
            {
                return false;
            }

            List<Thing> things = destination.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null
                    && thing.def != null
                    && thing.def.Fillage == FillCategory.Full
                    && (!(thing is Building_Door door) || !door.Open))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetImpactNormal(
            Thing target,
            Vector3 origin,
            Vector3 targetPosition,
            out Vector3 normal,
            out Vector3 impactPosition)
        {
            return TryGetImpactGeometry(
                target,
                origin,
                targetPosition,
                out normal,
                out impactPosition,
                out _);
        }

        public static bool TryGetImpactGeometry(
            Thing target,
            Vector3 origin,
            Vector3 targetPosition,
            out Vector3 normal,
            out Vector3 impactPosition,
            out Vector3 exitPosition)
        {
            normal = Vector3.zero;
            impactPosition = Vector3.zero;
            exitPosition = Vector3.zero;
            if (target == null)
            {
                return false;
            }

            Vector3 direction = targetPosition - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            CellRect occupiedRect = target.OccupiedRect();
            float minX = occupiedRect.minX;
            float maxX = occupiedRect.maxX + 1f;
            float minZ = occupiedRect.minZ;
            float maxZ = occupiedRect.maxZ + 1f;

            float xEntry;
            float xExit;
            if (direction.x == 0f)
            {
                if (origin.x < minX || origin.x > maxX)
                {
                    return false;
                }

                xEntry = float.NegativeInfinity;
                xExit = float.PositiveInfinity;
            }
            else if (direction.x > 0f)
            {
                xEntry = (minX - origin.x) / direction.x;
                xExit = (maxX - origin.x) / direction.x;
            }
            else
            {
                xEntry = (maxX - origin.x) / direction.x;
                xExit = (minX - origin.x) / direction.x;
            }

            float zEntry;
            float zExit;
            if (direction.z == 0f)
            {
                if (origin.z < minZ || origin.z > maxZ)
                {
                    return false;
                }

                zEntry = float.NegativeInfinity;
                zExit = float.PositiveInfinity;
            }
            else if (direction.z > 0f)
            {
                zEntry = (minZ - origin.z) / direction.z;
                zExit = (maxZ - origin.z) / direction.z;
            }
            else
            {
                zEntry = (maxZ - origin.z) / direction.z;
                zExit = (minZ - origin.z) / direction.z;
            }

            float entry = Mathf.Max(xEntry, zEntry);
            float exit = Mathf.Min(xExit, zExit);
            if (entry > exit || exit < 0f || entry > 1f)
            {
                return false;
            }

            if (xEntry > zEntry)
            {
                normal = direction.x > 0f ? Vector3.left : Vector3.right;
            }
            else
            {
                normal = direction.z > 0f ? Vector3.back : Vector3.forward;
            }

            impactPosition = origin + direction * Mathf.Max(0f, entry);
            impactPosition.y = 0f;
            exitPosition = origin + direction * Mathf.Max(0f, exit);
            exitPosition.y = 0f;

            return true;
        }

        public static float FindWeaponRange(ThingDef projectileDef, Thing launcher, Thing equipment)
        {
            List<Verb> candidateVerbs = FindCandidateVerbs(launcher, equipment);
            if (candidateVerbs == null)
            {
                return 0f;
            }

            for (int i = 0; i < candidateVerbs.Count; i++)
            {
                Verb_LaunchProjectile projectileVerb = candidateVerbs[i] as Verb_LaunchProjectile;
                if (projectileVerb != null && projectileVerb.Projectile == projectileDef)
                {
                    return projectileVerb.EffectiveRange;
                }
            }

            return 0f;
        }

        private static List<Verb> FindCandidateVerbs(Thing launcher, Thing equipment)
        {
            CompEquippable equipmentComp = equipment?.TryGetComp<CompEquippable>();
            if (equipmentComp != null)
            {
                return equipmentComp.AllVerbs;
            }

            if (equipment is Building_TurretGun buildingTurret && buildingTurret.GunCompEq != null)
            {
                return buildingTurret.GunCompEq.AllVerbs;
            }

            CompTurretGun turretComp = launcher?.TryGetComp<CompTurretGun>();
            return turretComp?.GunCompEq?.AllVerbs;
        }
    }
}
