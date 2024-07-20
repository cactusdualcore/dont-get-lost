﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory_quickbar_slot : inventory_slot
{
    public int number;

    public override void update(item item, int count, inventory inventory)
    {
        base.update(item, count, inventory);

        var player = inventory.GetComponentInParent<player>();
        if (player != null && player == player.current)
        {
            if (number == player.current.slot_number_equipped)
                player.current.validate_equip();
            toolbar_display_slot.update(number, item, count);
        }
    }
}
