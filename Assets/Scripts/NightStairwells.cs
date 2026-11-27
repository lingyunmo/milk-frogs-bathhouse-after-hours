using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        const float FloorRise = 3.6f;
        const float ShaftWidth = 3.90f;
        const float TurnDepth = 6.4f;
        const float BackWall = 9.95f;
        const float FlightWidth = 1.94f;
        const float FlightOffset = .98f;

        void FinishStairwell(Transform stair, int direction)
        {
            if (!stair) return;
            var entrance = stair.Find("近廊平台");
            if (!entrance) return;

            float floor = entrance.position.y + entrance.localScale.y * .5f;
            float mouth = entrance.position.z - direction * entrance.localScale.z * .5f;
            float takeoff = entrance.localScale.z;
            Vector3 At(float across, float height, float depth) =>
                new Vector3(entrance.position.x + across, height, mouth + direction * depth);

            if (floor < -1)
            {
                // Join the basement landing to a continuous slab under both flights.
                var footing = Box("梯井封底地坪", At(0, floor - .10f, (takeoff + BackWall) * .5f),
                    new Vector3(ShaftWidth, .20f, BackWall - takeoff), plaster, stair);
                Surface(footing, plaster, 1.62f, floor: true);
            }

            var landing = stair.Find("转向平台");
            float endOfFlight = landing
                ? direction * (landing.position.z - mouth) - landing.localScale.z * .5f
                : TurnDepth;
            float dividerHeight = landing ? FloorRise : 1.36f;
            var divider = Box("梯段中隔墙", At(0, floor + dividerHeight * .5f, (takeoff + endOfFlight) * .5f),
                new Vector3(.16f, dividerHeight, endOfFlight - takeoff), plaster, stair);
            Surface(divider, plaster, 1.45f);

            if (!landing)
            {
                // The top level has a descending flight only. Close the unused lane across its lip.
                var guard = Box("顶层楼梯端墙", At(-FlightOffset, floor + .68f, takeoff),
                    new Vector3(FlightWidth, 1.36f, .18f), plaster, stair);
                Surface(guard, plaster, 1.45f);
                return;
            }

            // Keep the junction with each flight where it was and carry the far landing to the rear wall.
            landing.position = At(0, landing.position.y, (endOfFlight + BackWall) * .5f);
            landing.localScale = new Vector3(ShaftWidth, landing.localScale.y, BackWall - endOfFlight);
            // The visible steps touch the wall. The player walks on a smooth surface of the same width.
            foreach (Transform step in stair)
            {
                if (step.name != "上行" && step.name != "折返") continue;
                float side = step.name == "上行" ? -1 : 1;
                step.position = At(side * FlightOffset, step.position.y, direction * (step.position.z - mouth));
                step.localScale = new Vector3(FlightWidth, step.localScale.y, step.localScale.z);
                step.GetComponent<Collider>().enabled = false;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float lane = side * FlightOffset;
                Vector3 start = At(lane, floor + (side > 0 ? FloorRise : 0), takeoff - .06f);
                Vector3 end = At(lane, floor + FloorRise * .5f, endOfFlight + .08f);
                Vector3 run = (end - start) * direction;
                Quaternion rotation = Quaternion.LookRotation(run, Vector3.up);
                const float thickness = .11f;
                var ramp = CollisionBox(stair, "梯段碰撞面", (start + end) * .5f - rotation * Vector3.up * (thickness * .5f),
                    new Vector3(FlightWidth, thickness, run.magnitude));
                ramp.rotation = rotation;
            }
        }
    }
}
