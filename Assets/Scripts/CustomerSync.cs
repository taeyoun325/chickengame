using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>손님 머리 위 주문 표시를 접속한 플레이어에게도 보여준다.</summary>
[RequireComponent(typeof(Customer))]
public sealed class CustomerSync : NetworkBehaviour
{
    private readonly NetworkVariable<FixedString64Bytes> netTag = new NetworkVariable<FixedString64Bytes>();

    private Customer customer;
    private string applied = string.Empty;

    private void Awake()
    {
        customer = GetComponent<Customer>();
    }

    private void Update()
    {
        if (customer == null)
        {
            return;
        }

        if (IsServer)
        {
            string current = customer.CurrentTag;
            if (current.Length > 40)
            {
                current = current.Substring(0, 40);
            }

            if (netTag.Value.ToString() != current)
            {
                netTag.Value = new FixedString64Bytes(current);
            }

            return;
        }

        string incoming = netTag.Value.ToString();
        if (applied != incoming)
        {
            applied = incoming;
            customer.ShowTag(incoming);
        }
    }
}
