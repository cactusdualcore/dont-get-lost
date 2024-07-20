﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : MonoBehaviour, IItemCollection
{
    public item_input input;
    public item_output overflow_output;

    public delegate bool item_accept_func(item i);
    public item_accept_func accept_item = (i) => true;

    item_locator[] locators;

    public bool has_items_to_dispense
    {
        get
        {
            foreach (var l in locators)
                if (l.item != null)
                    return true;
            return false;
        }
    }

    public item dispense_first_item()
    {
        foreach (var l in locators)
            if (l.item != null)
                return l.release_item();
        return null;
    }

    void Start()
    {
        if (input == null)
            throw new System.Exception("Item dispenser has no input!");

        locators = GetComponentsInChildren<item_locator>();
        if (locators.Length == 0)
            throw new System.Exception("Item dispenser has no locators!");
    }

    HashSet<item> overflow_items = new HashSet<item>();

    private void Update()
    {
        if (input.item_count > 0)
        {
            if (can_add(input.peek_item(0), 1, out item_locator locator))
                add(input.release_item(0), 1);
            else if (overflow_output != null)
                overflow_items.Add(input.release_item(0));
        }

        foreach (var i in new List<item>(overflow_items))
        {
            if (overflow_output == null)
            {
                item_rejector.create(i);
                continue;
            }

            if (utils.move_towards_and_look(i.transform,
                overflow_output.transform.position,
                Time.deltaTime, level_look: false))
            {
                overflow_output.add_item(i);
                overflow_items.Remove(i);
            }
        }
    }

    //#################//
    // IItemCollection //
    //#################//

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var l in locators)
            if (l.item != null)
            {
                var i = Resources.Load<item>("items/" + l.item.name);
                if (ret.ContainsKey(i)) ret[i] += 1;
                else ret[i] = 1;
            }
        return ret;
    }

    bool can_add(item i, int count, out item_locator locator)
    {
        locator = null;

        if (count != 1)
            throw new System.Exception("Items should be added to dispensers one at a time!");

        if (!accept_item(i)) return false;

        foreach (var l in locators)
            if (l.item == null)
            {
                locator = l;
                break;
            }

        if (locator == null) return false;
        return true;
    }

    public bool add(item itm, int count)
    {
        if (count != 1)
            throw new System.Exception("Items should be added to dispensers one at a time!");

        // Reject unacceptable items, or if there are no outputs
        if (!can_add(itm, count, out item_locator locator))
        {
            // Reject item (give to overflow output if we have one)
            if (overflow_output == null)
                Destroy(itm.gameObject);
            else
            {
                Vector3 delta = overflow_output.transform.position - itm.transform.position;
                delta.x = 0; delta.z = 0;
                itm.transform.position += delta;
                overflow_items.Add(itm);
            }
            return false;
        }

        locator.item = itm;
        return true;
    }

    public bool remove(item i, int count)
    {
        foreach (var l in locators)
            if (l.item?.name == i.name)
            {
                Destroy(l.release_item().gameObject);
                if (--count <= 0) return true;
            }
        return false;
    }
}
