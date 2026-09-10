using UnityEngine;

public sealed class PlayerInteraction : MonoBehaviour
{
    public const float InteractRange = 2.4f;
    private const float PickupRange = 2f;

    /// <summary>조준선으로 겨냥할 때의 사거리. 몸이 닿는 거리보다 조금 길어야
    /// 카운터 너머의 스테이션도 자연스럽게 잡힌다.</summary>
    private const float AimRange = 3.1f;

    private Transform holdPoint;
    private FoodItem heldFood;
    private GameObject extinguisher;

    /// <summary>이 플레이어가 바를 소스. 스테이션이 아니라 사람이 고른다.
    /// 스테이션에 두면 다른 사람이 바꿔 버려 엉뚱한 양념이 발린다.</summary>
    public MenuKind SauceChoice { get; private set; } = MenuKind.Seasoned;

    public void SetSauceChoice(MenuKind kind)
    {
        SauceChoice = kind;
    }

    /// <summary>지금 조준선이 물고 있는 스테이션. 이름표와 화면 안내가 같은 값을 봐야
    /// 표시와 실제 동작이 어긋나지 않는다.</summary>
    public static Station AimedStation { get; private set; }

    public FoodItem HeldFood => heldFood;
    public bool CarryingExtinguisher => extinguisher != null;
    public bool HandsFree => heldFood == null && extinguisher == null;

    private void Awake()
    {
        GameObject holdObject = new GameObject("Hold Point");
        holdObject.transform.SetParent(transform, false);
        // 1인칭이라 손은 눈보다 아래, 화면 오른쪽에 있어야 시야를 가리지 않고 보인다.
        holdObject.transform.localPosition = new Vector3(0.42f, 0.3f, 0.85f);
        holdPoint = holdObject.transform;
        WorldRegistry.Register(this);
    }

    private void OnDestroy()
    {
        WorldRegistry.Unregister(this);
    }

    /// <summary>조준 판정은 프레임마다 한 번이면 된다. 이름표 수십 개가 각자 쏘면 낭비다.</summary>
    private void Update()
    {
        if (FirstPersonView.IsSubject(transform))
        {
            AimedStation = FindNearbyStation();
        }
    }

    /// <summary>내가 보고 있는 캐릭터. 화면 안내가 이 사람 기준으로 문장을 만든다.</summary>
    public static PlayerInteraction Local
    {
        get
        {
            Transform subject = FirstPersonView.Subject;
            return subject != null ? subject.GetComponent<PlayerInteraction>() : null;
        }
    }

    /// <summary>F 키/게임패드 X. 네트워크에서는 호스트에 요청한다.</summary>
    public void RequestDrop()
    {
        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestDrop();
        }
        else
        {
            DropEverything();
        }
    }

    public void RequestSauceSwitch()
    {
        Station station = FindNearbyStation();
        if (station == null || station.stationType != StationType.Sauce)
        {
            return;
        }

        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestSauceCycle();
        }
        else if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.CycleSauce(this);
        }
    }

    /// <summary>업그레이드 데스크 앞에 있을 때만 구매가 된다.</summary>
    public void RequestUpgrade(int slot)
    {
        Station station = FindNearbyStation();
        if (station == null || station.stationType != StationType.Upgrade || RestaurantGame.Instance == null)
        {
            return;
        }

        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestUpgrade(slot);
        }
        else
        {
            RestaurantGame.Instance.BuyUpgrade(slot);
        }
    }

    public void SetHeldFood(FoodItem food)
    {
        heldFood = food;
        if (food.RestingStation != null)
        {
            food.RestingStation.Clear();
        }

        NetworkAttach.Parent(food, holdPoint);
        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
        food.SetHeld(true);
    }

    /// <summary>쓰레기통에 버렸을 때처럼 들고 있던 음식이 사라진 경우.</summary>
    public void ClearHeldFood()
    {
        heldFood = null;
    }

    /// <summary>들고 있던 음식을 스테이션 위에 올려둔다.</summary>
    public FoodItem ReleaseHeldFoodTo(Station station)
    {
        if (heldFood == null)
        {
            return null;
        }

        FoodItem released = heldFood;
        heldFood = null;
        NetworkAttach.Unparent(released);
        station.Place(released);
        released.SetHeld(false);
        return released;
    }

    /// <summary>손이 비어 있으면 근처에 떨어진 음식을 줍는다.</summary>
    public void TryPickUpNearbyFood()
    {
        if (!HandsFree)
        {
            return;
        }

        FoodItem nearbyFood = FindNearbyFood();
        if (nearbyFood == null)
        {
            return;
        }

        SetHeldFood(nearbyFood);
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage($"{nearbyFood.Describe()}을 들었습니다");
        }
    }

    public void TakeExtinguisher()
    {
        if (extinguisher != null)
        {
            return;
        }

        extinguisher = GameMaterials.CreatePrimitive(PrimitiveType.Cylinder, "Extinguisher", new Color(0.85f, 0.1f, 0.1f));
        extinguisher.transform.SetParent(holdPoint, false);
        extinguisher.transform.localPosition = Vector3.zero;
        extinguisher.transform.localScale = new Vector3(0.3f, 0.45f, 0.3f);
        Destroy(extinguisher.GetComponent<Collider>());
    }

    public void ReturnExtinguisher()
    {
        if (extinguisher != null)
        {
            Destroy(extinguisher);
            extinguisher = null;
        }
    }

    /// <summary>미끄러지거나 F 키를 누르면 들고 있던 것을 바닥에 떨어뜨린다.</summary>
    public void DropEverything()
    {
        ReturnExtinguisher();

        if (heldFood == null)
        {
            return;
        }

        FoodItem dropped = heldFood;
        heldFood = null;
        NetworkAttach.Unparent(dropped);
        Vector3 spot = transform.position + transform.forward * 0.8f;
        spot.y = 0.35f;
        dropped.transform.position = spot;

        // 떨어진 치킨은 실제로 굴러가도록 물리를 붙인다.
        Rigidbody body = dropped.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = dropped.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.6f;
        }

        dropped.SetHeld(false);
        body.linearVelocity = transform.forward * 2f;
        dropped.MarkDirty();

        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage("떨어뜨렸습니다! 바닥에 닿은 치킨은 못 씁니다");
        }
    }

    /// <summary>발밑에 기름이 있고 손이 비었을 때만 닦는다. 손에 뭘 들고 있으면
    /// 평소대로 스테이션과 상호작용하므로, 기름이 조리 동선을 가로막지 않는다.</summary>
    public bool CanClean => HandsFree && OilPuddle.Covering(transform.position) != null;

    public void Interact()
    {
        if (RestaurantGame.Instance == null)
        {
            return;
        }

        bool client = KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer;

        if (extinguisher != null)
        {
            if (client)
            {
                KitchenNetwork.Instance.RequestExtinguish();
                return;
            }

            if (RestaurantGame.Instance.TryExtinguishNearby(transform.position))
            {
                return;
            }
        }

        if (CanClean)
        {
            if (client)
            {
                KitchenNetwork.Instance.RequestClean();
            }
            else
            {
                RestaurantGame.Instance.TryCleanNearby(transform.position);
            }

            return;
        }

        Station station = FindNearbyStation();

        // 접속한 손님이면 호스트에 요청하고, 호스트/싱글이면 바로 처리한다.
        if (client)
        {
            if (station != null)
            {
                KitchenNetwork.Instance.RequestInteract(station.Index);
            }
            else
            {
                KitchenNetwork.Instance.RequestPickup();
            }

            return;
        }

        if (station != null)
        {
            RestaurantGame.Instance.InteractWithStation(this, station);
            return;
        }

        TryPickUpNearbyFood();
    }

    /// <summary>조준선이 스테이션을 물고 있으면 그것이 대상이다. 아무 데나 보고 있으면
    /// 예전처럼 가까운 스테이션으로 떨어져, 급할 때 조준을 강요하지 않는다.</summary>
    public Station FindNearbyStation()
    {
        if (TryAim(out RaycastHit hit))
        {
            Station aimed = hit.collider.GetComponentInParent<Station>();
            if (aimed != null && aimed.gameObject.activeInHierarchy)
            {
                return aimed;
            }
        }

        return WorldRegistry.NearestStation(transform.position, InteractRange, transform.forward);
    }

    private FoodItem FindNearbyFood()
    {
        if (TryAim(out RaycastHit hit))
        {
            FoodItem aimed = hit.collider.GetComponentInParent<FoodItem>();
            // 스테이션에 올려진 음식은 스테이션 쪽 절차를 거쳐야 한다.
            if (aimed != null && aimed.RestingStation == null && aimed.state != FoodState.Frying)
            {
                return aimed;
            }
        }

        return WorldRegistry.NearestLooseFood(transform.position, PickupRange);
    }

    /// <summary>내 눈에서 조준선 방향으로 쏘는 광선. 남의 아바타는 조준하지 않는다.</summary>
    private bool TryAim(out RaycastHit hit)
    {
        hit = default;
        if (!FirstPersonView.IsSubject(transform))
        {
            return false;
        }

        return Physics.Raycast(FirstPersonView.AimRay, out hit, AimRange, ~0, QueryTriggerInteraction.Ignore);
    }
}
