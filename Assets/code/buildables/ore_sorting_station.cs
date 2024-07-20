using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ore_sorting_station : character_interactable_options
{
    public item_output primary_output;
    public item_output secondary_output;
    public List<item> ore_level_products = new List<item>();
    item_input input;

    const float NEXT_ORE_PROB = 0.5f;
    const float PRODUCT_PROB = 0.5f;

    public override string task_summary() => "Sorting ore";
    protected override string options_title => "Sorting recipes";
    protected override int options_count => 1;

    protected override option get_option(int i)
    {
        if (i != 0)
            Debug.LogError("Unexpected option in ore sorter!");

        return new option
        {
            sprite = Resources.Load<Sprite>("sprites/mixed_ore"),
            text = "Sort ore"
        };
    }

    protected override bool ready_to_assign(character c) => base.ready_to_assign(c) && input.item_count > 0;
    bool sortable_item(item i) => i.name.StartsWith("mixed_ore") || i.name.StartsWith("sorted_ore");

    protected override void Start()
    {
        base.Start();
        input = GetComponentInChildren<item_input>();
    }

    float time_working = 0;
    float work_done => time_working * current_proficiency.total_multiplier;

    protected override void on_arrive(character c)
    {
        base.on_arrive(c);
        time_working = 0;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        time_working += Time.deltaTime;
        if (work_done < stage + 1) return STAGE_RESULT.STAGE_UNDERWAY;

        var itm = input.release_next_item();
        if (itm != null)
        {
            if (!sortable_item(itm))
            {
                // Wasn't sortable - discard
                item_dropper.create(itm);
            }
            else
            {
                // Sort
                Destroy(itm.gameObject);

                var words = itm.name.Split('_');
                if (!int.TryParse(words[words.Length - 1], out int item_level))
                    item_level = 0;

                if (Random.Range(0, 1f) < NEXT_ORE_PROB)
                {
                    // Create the next tier ore
                    var next_ore = sorted_ore_from_level(item_level + 1);
                    if (next_ore != null)
                        secondary_output.add(next_ore, 1);
                }

                if (Random.Range(0, 1f) < PRODUCT_PROB)
                {
                    // Create the output item
                    if (item_level >= 0 && item_level < ore_level_products.Count)
                        primary_output.add(ore_level_products[item_level], 1);
                }
            }
        }

        return time_working > 10f ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_COMPLETE;
    }

    public static item sorted_ore_from_level(int level)
    {
        if (level == 0) return Resources.Load<item>("items/mixed_ore");
        return Resources.Load<item>("items/sorted_ore_" + level);
    }

    class sorting_recipe_info : IRecipeInfo
    {
        item input_ore;
        item output_ore;
        item product;

        public sorting_recipe_info(int level, List<item> products)
        {
            input_ore = sorted_ore_from_level(level);
            output_ore = sorted_ore_from_level(level + 1);
            product = level >= 0 && level < products.Count ? products[level] : null;
        }

        public string recipe_book_string() =>
            (product == null ? "" : product.display_name + " + ") + 
            output_ore.display_name + " < " + 
            input_ore.display_name;

        public float average_amount_produced(item i)
        {
            if (i == null) return 0;
            if (output_ore != null && i.name == output_ore.name) return NEXT_ORE_PROB;
            if (product != null && i.name == product.name) return PRODUCT_PROB;
            return 0;
        }

        public float average_ingredients_value() => input_ore == null ? 0f : input_ore.value;
    }

    public List<IRecipeInfo> recipe_infos()
    {
        var ret = new List<IRecipeInfo>();
        for (int i = 0; i < ore_level_products.Count; ++i)
            ret.Add(new sorting_recipe_info(i, ore_level_products));
        return ret;
    }
}
