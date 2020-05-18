﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace Avali
{
	public class JoyGiver_SocialRelaxAvali : JoyGiver
	{
		private static List<CompGatherSpot> workingSpots = new List<CompGatherSpot>();

		private const float GatherRadius = 3.9f;

		private static readonly int NumRadiusCells = GenRadial.NumCellsInRadius(3.9f);

		private static readonly List<IntVec3> RadialPatternMiddleOutward = (from c in GenRadial.RadialPattern.Take(JoyGiver_SocialRelaxAvali.NumRadiusCells)
		orderby Mathf.Abs((c - IntVec3.Zero).LengthHorizontal - 1.95f)
		select c).ToList<IntVec3>();

		private static List<ThingDef> nurseableDrugs = new List<ThingDef>();
		
		public override Job TryGiveJob(Pawn pawn)
		{
			return TryGiveJobInt(pawn, null);
		}

		public override Job TryGiveJobInGatheringArea(Pawn pawn, IntVec3 gatherSpot)
		{
			return TryGiveJobInt(pawn, (CompGatherSpot x) => GatheringsUtility.InGatheringArea(x.parent.Position, gatherSpot, pawn.Map));
		}

		private Job TryGiveJobInt(Pawn pawn, Predicate<CompGatherSpot> gatherSpotValidator)
		{
			if (!pawn.health.hediffSet.HasHediff(HediffDefOf.AvaliBiology, false)) return null;
			if (pawn.Map.gatherSpotLister.activeSpots.Count == 0)
			{
				return null;
			}
			JoyGiver_SocialRelaxAvali.workingSpots.Clear();
			for (int i = 0; i < pawn.Map.gatherSpotLister.activeSpots.Count; i++)
			{
				JoyGiver_SocialRelaxAvali.workingSpots.Add(pawn.Map.gatherSpotLister.activeSpots[i]);
			}
			CompGatherSpot compGatherSpot;
			while (JoyGiver_SocialRelaxAvali.workingSpots.TryRandomElement(out compGatherSpot))
			{
				JoyGiver_SocialRelaxAvali.workingSpots.Remove(compGatherSpot);
				if (!compGatherSpot.parent.IsForbidden(pawn))
				{
					if (pawn.CanReach(compGatherSpot.parent, PathEndMode.Touch, Danger.None, false, TraverseMode.ByPawn))
					{
						if (compGatherSpot.parent.IsSociallyProper(pawn))
						{
							if (compGatherSpot.parent.IsPoliticallyProper(pawn))
							{
								if (gatherSpotValidator == null || gatherSpotValidator(compGatherSpot))
								{
									Job job;
									Thing t2;
									if (compGatherSpot.parent.def.surfaceType == SurfaceType.Eat)
									{
										Thing t;
										if (!JoyGiver_SocialRelaxAvali.TryFindChairBesideTable(compGatherSpot.parent, pawn, out t))
										{
											return null;
										}
										job = new Job(def.jobDef, compGatherSpot.parent, t);
									}
									else if (JoyGiver_SocialRelaxAvali.TryFindChairNear(compGatherSpot.parent.Position, pawn, out t2))
									{
										job = new Job(def.jobDef, compGatherSpot.parent, t2);
									}
									else
									{
										IntVec3 c;
										if (!JoyGiver_SocialRelaxAvali.TryFindSitSpotOnGroundNear(compGatherSpot.parent.Position, pawn, out c))
										{
											return null;
										}
										job = new Job(def.jobDef, compGatherSpot.parent, c);
									}
									Thing thing;
									if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && JoyGiver_SocialRelaxAvali.TryFindIngestibleToNurse(compGatherSpot.parent.Position, pawn, out thing))
									{
										job.targetC = thing;
										job.count = Mathf.Min(thing.stackCount, thing.def.ingestible.maxNumToIngestAtOnce);
									}
									return job;
								}
							}
						}
					}
				}
			}
			return null;
		}

		private static bool TryFindIngestibleToNurse(IntVec3 center, Pawn ingester, out Thing ingestible)
		{
			if (ingester.IsTeetotaler())
			{
				ingestible = null;
				return false;
			}
			if (ingester.drugs == null)
			{
				ingestible = null;
				return false;
			}
			JoyGiver_SocialRelaxAvali.nurseableDrugs.Clear();
			DrugPolicy currentPolicy = ingester.drugs.CurrentPolicy;
			for (int i = 0; i < currentPolicy.Count; i++)
			{
				if (currentPolicy[i].allowedForJoy && currentPolicy[i].drug.ingestible.nurseable)
				{
					JoyGiver_SocialRelaxAvali.nurseableDrugs.Add(currentPolicy[i].drug);
				}
			}
			JoyGiver_SocialRelaxAvali.nurseableDrugs.Shuffle<ThingDef>();
			for (int j = 0; j < JoyGiver_SocialRelaxAvali.nurseableDrugs.Count; j++)
			{
				List<Thing> list = ingester.Map.listerThings.ThingsOfDef(JoyGiver_SocialRelaxAvali.nurseableDrugs[j]);
				if (list.Count > 0)
				{
					Predicate<Thing> validator = (Thing t) => ingester.CanReserve(t, 1, -1, null, false) && !t.IsForbidden(ingester);
					ingestible = GenClosest.ClosestThing_Global_Reachable(center, ingester.Map, list, PathEndMode.OnCell, TraverseParms.For(ingester, Danger.Deadly, TraverseMode.ByPawn, false), 40f, validator, null);
					if (ingestible != null)
					{
						return true;
					}
				}
			}
			ingestible = null;
			return false;
		}

		private static bool TryFindChairBesideTable(Thing table, Pawn sitter, out Thing chair)
		{
			for (int i = 0; i < 30; i++)
			{
				IntVec3 c = table.RandomAdjacentCellCardinal();
				Building edifice = c.GetEdifice(table.Map);
				if (edifice != null && edifice.def.building.isSittable && sitter.CanReserve(edifice, 1, -1, null, false))
				{
					chair = edifice;
					return true;
				}
			}
			chair = null;
			return false;
		}

		private static bool TryFindChairNear(IntVec3 center, Pawn sitter, out Thing chair)
		{
			for (int i = 0; i < JoyGiver_SocialRelaxAvali.RadialPatternMiddleOutward.Count; i++)
			{
				IntVec3 c = center + JoyGiver_SocialRelaxAvali.RadialPatternMiddleOutward[i];
				Building edifice = c.GetEdifice(sitter.Map);
				if (edifice != null && edifice.def.building.isSittable && sitter.CanReserve(edifice, 1, -1, null, false) && !edifice.IsForbidden(sitter) && GenSight.LineOfSight(center, edifice.Position, sitter.Map, true, null, 0, 0))
				{
					chair = edifice;
					return true;
				}
			}
			chair = null;
			return false;
		}

		private static bool TryFindSitSpotOnGroundNear(IntVec3 center, Pawn sitter, out IntVec3 result)
		{
			for (int i = 0; i < 30; i++)
			{
				IntVec3 intVec = center + GenRadial.RadialPattern[Rand.Range(1, JoyGiver_SocialRelaxAvali.NumRadiusCells)];
				if (sitter.CanReserveAndReach(intVec, PathEndMode.OnCell, Danger.None, 1, -1, null, false) && intVec.GetEdifice(sitter.Map) == null && GenSight.LineOfSight(center, intVec, sitter.Map, true, null, 0, 0))
				{
					result = intVec;
					return true;
				}
			}
			result = IntVec3.Invalid;
			return false;
		}
	}
}
