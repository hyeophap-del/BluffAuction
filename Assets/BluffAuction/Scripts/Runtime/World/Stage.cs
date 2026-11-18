using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Characters;
using UnityEngine;

namespace BluffAuction.World
{
    /// <summary>
    /// 경매장 무대: 좌석의 손님들, 경매사, 회전판 위 물건. 게임 진행(Presenter)이 여기에 명령한다.
    /// 좌석 0 은 늘 '나' (카메라 쪽), 화면에는 손만 보이도록 몸은 숨긴다.
    /// </summary>
    public sealed class Stage : MonoBehaviour
    {
        public Hall Hall;
        public Guest Auctioneer;
        public readonly List<Guest> Guests = new List<Guest>();
        public int SeatCount;
        GameObject item;
        int itemAppearance = -1;
        float itemPop;
        float itemScale = 1f;
        Light itemGlow;

        public static readonly Color[] SeatColors =
        {
            new Color(0.91f, 0.30f, 0.30f), new Color(0.27f, 0.55f, 0.92f), new Color(0.30f, 0.74f, 0.42f), new Color(0.96f, 0.76f, 0.20f),
            new Color(0.62f, 0.38f, 0.86f), new Color(0.98f, 0.55f, 0.20f), new Color(0.25f, 0.78f, 0.80f), new Color(0.93f, 0.45f, 0.70f),
            new Color(0.60f, 0.45f, 0.30f), new Color(0.55f, 0.80f, 0.25f), new Color(0.45f, 0.48f, 0.90f), new Color(0.75f, 0.75f, 0.78f)
        };

        public const float GuestScale = 1.24f;

        public static Stage Create(Transform parent, Hall hall)
        {
            var go = new GameObject("Stage");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Stage>();
            s.Hall = hall;
            s.Auctioneer = GuestBuilder.Build(go.transform, CharacterLook.Auctioneer, Color.white, true);
            s.Auctioneer.transform.localScale = Vector3.one * 1.12f;
            s.Auctioneer.transform.localPosition = Hall.AuctioneerPos;
            s.Auctioneer.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var gl = new GameObject("ItemGlow");
            gl.transform.SetParent(hall.ItemAnchor, false);
            gl.transform.localPosition = new Vector3(0, 0.25f, -0.25f);
            s.itemGlow = gl.AddComponent<Light>();
            s.itemGlow.type = LightType.Point;
            s.itemGlow.range = 0.9f;
            s.itemGlow.intensity = 0f;
            s.itemGlow.color = new Color(1f, 0.9f, 0.7f);
            return s;
        }

        /// <summary>내 좌석 (카메라 쪽 자리에 앉는다). 온라인 참가자는 0 이 아닐 수 있다.</summary>
        public int LocalSeat;

        /// <summary>좌석 k 가 놓이는 탁자 위 자리 (내 좌석이 늘 카메라 쪽 0 번 자리).</summary>
        public int Visual(int k) { return SeatCount > 0 ? ((k - LocalSeat) % SeatCount + SeatCount) % SeatCount : k; }

        /// <summary>좌석과 손님을 다시 만든다. looks[k] 는 좌석 k 의 생김새, colors 는 좌석 색 번호.</summary>
        public void Seat(IList<CharacterLook> looks, IList<int> colors, int localSeat = 0)
        {
            foreach (var g in Guests) if (g != null) Destroy(g.gameObject);
            Guests.Clear();
            SeatCount = looks.Count;
            LocalSeat = Mathf.Clamp(localSeat, 0, Mathf.Max(0, SeatCount - 1));
            Hall.ArrangeChairs(SeatCount);
            for (int k = 0; k < SeatCount; k++)
            {
                var c = SeatColors[colors[k] % SeatColors.Length];
                var g = GuestBuilder.Build(transform, looks[k], c);
                int vk = Visual(k);
                // 탁자에 비해 잘 보이도록 키우고, 엉덩이가 방석(0.53m)에 닿게 내린다
                g.transform.localScale = Vector3.one * GuestScale;
                g.transform.localPosition = Hall.SeatPos(vk, SeatCount, Hall.SeatR) + Vector3.up * (0.53f - 0.5f * GuestScale);
                g.transform.localRotation = Hall.SeatRot(vk, SeatCount);
                // 시선 맞추기·초점용 몸통 충돌체
                var col = g.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.95f, 0);
                col.height = 1.1f;
                col.radius = 0.24f;
                col.isTrigger = true;
                if (k == LocalSeat)
                {
                    g.LocalPose = true;
                    HideSelf(g);
                    col.enabled = false;
                }
                Guests.Add(g);
            }
        }

        /// <summary>1인칭: 내 머리(카메라가 들어가는 곳)와 팔은 그림자만 남긴다 (눈 바로 아래 손이 크게 흐려져 화면을 가리므로).
        /// 패들은 TableKit 이 탁자 위로 옮겨 다시 보이게 한다.</summary>
        static void HideSelf(Guest g)
        {
            foreach (var part in new[] { g.Head, g.ShoulderL, g.ShoulderR })
            {
                if (part == null) continue;
                foreach (var r in part.GetComponentsInChildren<MeshRenderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            // 감정 표현 중에는 왼팔이 보인다 (Guest.LocalArms)
            g.ArmsHidden = true;
        }

        public Vector3 SeatHead(int k)
        {
            if (k < 0 || k >= Guests.Count) return Vector3.up;
            return Guests[k].Head.position + Vector3.up * 0.12f;
        }

        public Vector3 SeatWorld(int k)
        {
            return transform.TransformPoint(Hall.SeatPos(Visual(k), SeatCount, Hall.SeatR));
        }

        public Vector3 ItemPoint { get { return Hall.ItemAnchor.position + Vector3.up * 0.16f; } }

        public void ShowItem(int appearance)
        {
            if (appearance == itemAppearance && item != null && itemPicture == null) return;
            HideItem();
            if (appearance < 0) return;
            item = ItemModels.Build(appearance, Hall.ItemAnchor);
            itemAppearance = appearance;
            itemPop = 0f;
            itemScale = item.transform.localScale.x;
            item.transform.localScale = Vector3.zero;
        }

        Texture itemPicture;

        /// <summary>경매품: 직접 넣은 그림이 있으면 금테 액자로, 없으면 고른 모형으로.</summary>
        public void ShowLot(int appearance, Texture picture)
        {
            if (picture == null) { ShowItem(appearance); return; }
            if (itemPicture == picture && item != null) return;
            HideItem();
            item = ItemModels.BuildPicture(picture, Hall.ItemAnchor);
            itemPicture = picture;
            itemAppearance = -2;
            itemPop = 0f;
            itemScale = item.transform.localScale.x;
            item.transform.localScale = Vector3.zero;
        }

        public void HideItem()
        {
            if (item != null) Destroy(item);
            item = null;
            itemAppearance = -1;
            itemPicture = null;
        }

        void Update()
        {
            if (item != null)
            {
                itemPop = Mathf.Min(1f, itemPop + Time.deltaTime * 2.2f);
                // 튀어나오듯 커졌다 제자리
                float e = itemPop < 1f ? 1f + Mathf.Sin(itemPop * Mathf.PI) * 0.18f * (1f - itemPop) : 1f;
                item.transform.localScale = Vector3.one * (Mathf.SmoothStep(0, 1, itemPop) * e * itemScale);
                itemGlow.intensity = Mathf.Lerp(itemGlow.intensity, 0.25f, Time.deltaTime * 2f);
            }
            else if (itemGlow != null) itemGlow.intensity = Mathf.Lerp(itemGlow.intensity, 0f, Time.deltaTime * 3f);
        }

        /// <summary>모두 한 곳을 보게 한다 (물건·입찰자·경매사).</summary>
        public void AllLookAt(Vector3 world, int except = -1)
        {
            for (int k = 0; k < Guests.Count; k++) if (k != except) Guests[k].LookAt(world);
        }
    }
}
