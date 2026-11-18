using UnityEngine;

namespace BluffAuction.Art
{
    /// <summary>코드로 만든 메시는 GameObject 를 지워도 남으므로, 오브젝트가 사라질 때 같이 지운다 (판마다 손님·물건을 새로 만든다).</summary>
    public sealed class MeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);
        }
    }

    /// <summary>한 번 쓰고 마는 재질 (그림 액자 등) 도 오브젝트와 함께 지운다.</summary>
    public sealed class MaterialOwner : MonoBehaviour
    {
        public Material Material;

        void OnDestroy()
        {
            if (Material != null) Destroy(Material);
        }
    }
}
