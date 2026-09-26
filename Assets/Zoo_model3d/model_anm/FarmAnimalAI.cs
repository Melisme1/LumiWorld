using System.Collections;
using UnityEngine;

public class FarmAnimalAI : MonoBehaviour
{
    private Animation anim;

    // Danh sách tên chính xác các Animation Clip có sẵn trong component Animation
    private string[] animNames = { "Idle", "Walk", "Run", "Jump", "Sit", "Howl", "Fetch", "Rest_Pose" };

    void Start()
    {
        anim = GetComponent<Animation>();
        
        // Bắt đầu vòng lặp chuyển đổi animation ngẫu nhiên
        StartCoroutine(AutoPlayRandomAnimation());
    }

    IEnumerator AutoPlayRandomAnimation()
    {
        while (true)
        {
            // Chọn ngẫu nhiên 1 tên animation
            string randomAnim = animNames[Random.Range(0, animNames.Length)];

            // Kiểm tra xem animation đó có tồn tại trong danh sách không rồi mới phát
            if (anim.GetClip(randomAnim) != null)
            {
                // CrossFade giúp chuyển từ hành động cũ sang hành động mới mượt mà trong 0.3s
                anim.CrossFade(randomAnim, 0.3f);
            }

            // Chờ ngẫu nhiên từ 3 đến 6 giây trước khi đổi sang hành động tiếp theo
            yield return new WaitForSeconds(Random.Range(3f, 6f));
        }
    }
}