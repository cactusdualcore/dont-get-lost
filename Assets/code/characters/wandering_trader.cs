using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wandering_trader : trader, IExtendsNetworked
{
    public override int get_stock(string item) => stock[item];
    public override void set_stock(string item, int count) => stock[item] = count;
    public override string display_name() => "Wandering trader";

    public override Dictionary<string, int> get_stock()
    {
        Dictionary<string, int> ret = new Dictionary<string, int>();
        foreach (var kv in stock)
            ret[kv.Key] = kv.Value;
        return ret;
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_string_counts stock;

    public IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                stock = new networked_variables.net_string_counts();
                GetComponent<character>().add_register_listener(init_stock);
            }
        };
    }

    void init_stock()
    {
        if (stock.count == 0)
        {
            // Stock needs initializing
            stock["coin"] = 500;

            var itms = Resources.LoadAll<item>("items/");
            for (int n = 0; n < 25; ++n)
            {
                var i = itms[Random.Range(0, itms.Length)];

                if (i.GetComponentInChildren<not_available_from_trader>() != null)
                    continue;

                int val = Mathf.Max(i.value, 1);
                int count = Mathf.Max(200 / val, 1);
                stock[i.name] = count;
            }
        }
    }
}