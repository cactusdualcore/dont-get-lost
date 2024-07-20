﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dynamite : item
{
    public override player_interaction[] item_uses()
    {
        return new player_interaction[] { new demolish_use() };
    }

    class demolish_use : player_interaction
    {
        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            return "blow stuff up";
        }

        protected override bool on_start_interaction(player player)
        {
            // Only blow stuff up if we have authority
            if (!player.has_authority) return true;

            var ray = player.current.camera_ray(player.INTERACTION_RANGE, out float dis);

            if (Physics.Raycast(ray, out RaycastHit hit, dis))
            {
                var wo = hit.collider.GetComponentInParent<world_object>();
                if (wo != null)
                {
                    var wod = (world_object_destroyed)client.create(
                        wo.transform.position, "misc/world_object_destroyed");
                    wod.target_to_world_object(wo);
                }
            }

            return true;
        }
    }
}
