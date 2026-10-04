# 🌿 LUMIWORLD - BỘ THƯ VIỆN PROMPT 3D CHUẨN ART STYLE PRESERVE (GOLDEN STANDARD & VISUAL HIERARCHY)

> **✨ Quy luật Phân Cấp Thị Giác (4-Tier Visual Hierarchy):**  
> Để một ô lục giác (Hex Tile) vừa sống động, vừa có chiều sâu mà không bị rối mắt hay trơ trọi chỗ trống, mỗi Habitat được phân bổ chặt chẽ thành 4 tầng:
> - **👑 Tầng 1: Hero Asset (Variant A / Tâm Điểm):** Bề thế, hùng vĩ, thân to lực lưỡng, gốc bạnh vững chãi (`massive flared base`), tán xòe rộng gồm **6–8 cụm tán mây bồng bềnh (`overlapping cloud puffs`)**, làm tâm điểm cho cả ô đất.
> - **🌿 Tầng 2: Supporting Asset (Variant B, C / Bổ Trợ Dáng):** Thanh mảnh, dẻo dai, cành uốn lượn có nhịp điệu (`graceful organic curve`), tán gọn gàng 2–3 chùm để đung đưa theo gió và tôn vinh Hero Asset.
> - **🍀 Tầng 3: Ground Detail & Accent (Variant D, E, F, G, H, I / Điểm Xuyết Sát Đất):** Bụi dương xỉ, hoa chuông, gốc cây mục, đá cuội rêu, nấm rừng tạo độ chuyển tiếp tự nhiên.
> - **🌱 Tầng 4: Micro-Foliage & Ground Fillers (Variant J, K, L, M, N / Lấp Đầy Khe Trống):** Cỏ múp míp tí hon, đệm rêu phồng, lá sồi vàng rụng, cỏ ba lá, sỏi mini bẹp sát đất — đóng vai trò như chất lỏng len lỏi lấp đầy các khoảng đất trống trơ trọi.
>
> **🎨 Chuẩn mực Hình khối & Bề mặt:**
> - Bo tròn mịn màng như đám mây / kẹo dẻo (`buttery smooth rounded volumes, soft cloud curvature`), triệt tiêu 100% cạnh tam giác sắc nhọn (`zero sharp edges, zero visible polygon facets`).
> - Khoảng hở cành nhánh (`negative space`) thoáng đãng, tối ưu sẵn sàng cho **Wind Sway Shader** trong Unity.
> - Khung vuông **1:1 (`--ar 1:1`)**, căn giữa với **20% khoảng thở**, lơ lửng không dính đế/đất, nền trắng tinh khiết tuyệt đối.

---

## 🚫 1. CẤU HÌNH NEGATIVE PROMPT (BẮT BUỘC DÙNG KÈM)
*Dán vào ô **Negative Prompt** để loại bỏ hoàn toàn góc cạnh đa giác sắc nhọn, đất nặn bẹp dính và chân đế:*

```text
angular facets, sharp polygonal edges, flat shading, origami, geometric triangles, sharp ridges, hard planar surfaces, monolithic blob, melted plastic, play-doh lump, fused solid mass, stiff cylinder, pedestal, base, stand, platform, ground, ground plane, terrain base, dirt mound, island base, circular base, cylinder base, display stand, plinth, showcase base, floor, soil, tile, table, background scenery, environment, trees in background, hills, clouds, horizon, sky, landscape, cropped, cut off, touching the edge, out of frame, shadow casting ground, photorealistic, realistic textures, hyperrealistic, noisy bump map, detailed bark grain, alpha cards, transparent leaves, razor-sharp edges, gritty, grunge, dark gothic, high poly sculpt, floating geometry, wireframe, unpolished, text, watermark, symmetrical, bilateral symmetry, mirror symmetry, centered split trunk, umbrella shape, mushroom shape, twin trunks, identical sides, two legs, stiff vertical trunk, stilt roots, mangrove roots, aerial roots, spider roots, roots gripping air, floating trunk, tripod roots, standing on roots
```

---

## 🎨 2. MASTER STYLE MODIFIER (TỪ KHÓA BỔ SUNG CHO PROP MỚI)

```text
centered in frame, floating weightlessly in empty space with generous empty white padding on all sides. Isometric 3/4 perspective view. Focus exclusively on this single isolated 3D game asset, ignore all background, scenery, environment, and ground. Full view from top to bottom, nothing cropped or touching the borders. Clear empty air underneath with strictly no ground, no floor, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, buttery smooth marshmallow-like surfaces, soft curvature, zero sharp edges, zero visible polygon facets, curated warm pastel color palette, soft gradient shading, clean tactile finish, game-ready 3D asset --ar 1:1
```

---

# 🌲 PHẦN 1: STARTER ISLAND

---

### 1. Leafwood (Rừng Sồi Ôn Đới & Tán Lá Bồng Bềnh)
* **Bảng màu:** Xanh ngọc lục bảo nhạt (Sage/Jade Green), Vàng chanh nhẹ ở đỉnh tán, Nâu ấm vỏ cây hạt dẻ (Warm Chestnut Brown).

* **👑 Variant A (Hero Oak - Cây Đại Thụ Cổ Thụ Tán Sum Suê - GOLDEN STANDARD)**
  ```text
  A 3D video game item asset of a majestic ancient grand oak tree, colossal hero tree centerpiece, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Thick, powerful, massive ancient wooden trunk with wide flared root base, branching outward into multiple heavy sturdy boughs that support a grand, sprawling canopy made of numerous (6 to 8) overlapping, buttery smooth cloud-like foliage puff clusters forming a lush rounded dome. Rich depth and airy negative space between the thick boughs and layered canopies, allowing separate canopy clusters to sway in the breeze. Buttery smooth rounded surfaces, soft cloud curvature, zero sharp edges, zero visible polygon facets. Vibrant fresh sage green and warm sunlit lime gradients, rich warm chestnut bark, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the colossal tree prop, ignore all background and landscape. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom roots, nothing cropped. Clear empty air underneath, strictly floating, no ground, no floor, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, majestic, ancient, airy and lively --ar 1:1
  ```
  *Thông số:* ~550–700 tris | *Pivot:* Đáy tâm gốc cây (Y=0)

* **👑 Variant A2 (Hollow-Arch Great Oak - Đại Thụ Gốc Vòm Cổ Tích)**
  ```text
  A 3D video game item asset of a majestic ancient fantasy hollow oak tree, colossal hero tree centerpiece, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Massive ancient wooden trunk featuring a natural arched hollow tunnel at the base opening through to the other side, flanked by thick muscular flared root pillars gripping the air. Heavy upward-reaching boughs support a grand, sprawling canopy made of numerous (6 to 8) overlapping, buttery smooth cloud-like foliage puff clusters forming a lush airy dome. Clear negative space through the hollow trunk base and between canopy clusters. Buttery smooth rounded surfaces, soft cloud curvature, zero sharp edges, zero visible polygon facets. Fresh sage green with warm sunlit lime tips, warm chestnut bark with subtle velvet moss patinas, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the colossal tree prop, ignore all background and landscape. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom roots, nothing cropped. Clear empty air underneath, strictly floating, no ground, no floor, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, fairy-tale ancient grandeur, airy and lively --ar 1:1
  ```
  *Thông số:* ~600–750 tris | *Pivot:* Đáy tâm vòm gốc cây (Y=0)

* **👑 Variant A3 (Tiered Spreading Beech - Đại Thụ Dẻ Gai Tán Tầng Bồng Bềnh)**
  ```text
  A 3D video game item asset of a grand ancient spreading beech tree, colossal hero tree centerpiece, designed for gentle breeze sway animation, centered in frame, floating weightlessly in mid-air in empty space. Stout, sturdy smooth pale-gray wooden trunk with wide grounded root buttresses, branching horizontally into wide reaching boughs that hold three distinct stepped horizontal tiers of broad, flattened, buttery smooth cloud-like foliage pads fanning outward. Generous airy negative space between the horizontal foliage tiers, allowing separate canopy layers to sway gently in the wind. Buttery smooth rounded volumes, soft cloud curvature, zero sharp edges, zero visible polygon facets. Rich emerald and warm jade green gradients, clean smooth bark, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus only on the grand tree prop, ignore all terrain and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom roots, nothing cropped. Clear empty air underneath, strictly floating, no ground, no dirt mound, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, majestic sheltered canopy, calm and ancient --ar 1:1
  ```
  *Thông số:* ~550–700 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **👑 Variant A4 (Asymmetric Swept Oak - Cổ Thụ Dáng Huyền Tán Bay Bất Đối Xứng)**
  ```text
  A 3D video game item asset of a majestic ancient fantasy oak tree with dramatic organic asymmetry, colossal hero tree centerpiece, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. A powerful, gnarled ancient wooden trunk with a dynamic natural lean and wide uneven muscular root flare, branching into distinct asymmetric boughs: one side reaches boldly upward with high lofty cloud puff canopies, while the opposite heavy limb sweeps gracefully outward and lower to the side. Generous airy negative space and visual depth between the uneven branches, preventing any umbrella shape. Zero bilateral symmetry, zero mirror symmetry, dynamic sweeping silhouette visible from a high 3/4 top-down game camera angle. Buttery smooth rounded surfaces, soft cloud curvature, zero sharp edges, zero visible polygon facets. Fresh sage green and warm sunlit chartreuse gradients, warm chestnut bark with gentle velvety moss patches, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the colossal asymmetrical tree prop, ignore all background and landscape. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom roots, nothing cropped. Clear empty air underneath, strictly floating, no ground, no floor, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, dynamic windswept ancient grandeur, tactile finish --ar 1:1
  ```
  *Thông số:* ~550–720 tris | *Pivot:* Đáy tâm gốc cây (Y=0)

* **🌿 Variant B (Slender Curved Tree - Cây Dáng Nghiêng 3 Nhánh Duyên Dáng - PROVEN)**
  ```text
  A 3D video game item asset of a stylized organic oak tree, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Charming curved wooden trunk with natural graceful posture, branching into three distinct visible limbs that hold separate, airy, cloud-like foliage puff clusters. Beautiful negative space between the branches, light and dynamic silhouette. Buttery smooth rounded foliage volumes with soft cloud curvature, zero sharp edges, zero visible polygon facets. Vibrant fresh sage green and pastel lime gradients, warm smooth chestnut bark, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the tree prop, ignore all background and landscape. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom roots, nothing cropped. Clear empty air underneath, strictly floating, no ground, no floor, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, smooth marshmallow-like surfaces, airy and lively --ar 1:1
  ```
  *Thông số:* ~350–450 tris | *Pivot:* Đáy tâm gốc cây (Y=0)

* **🌿 Variant B2 (Weeping Woodland Willow - Liễu Rừng Cành Rủ Mềm Mại)**
  ```text
  A 3D video game item asset of an elegant stylized weeping woodland tree, designed for graceful wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Slender organic curved trunk leaning slightly with natural poise, graceful arching branches that cascade downward into multiple (4 to 5) elongated, drooping teardrop cloud-like foliage clusters hanging toward the base. Beautiful negative space between the cascading weeping puff tiers, ready for soft pendulum wind sway motion. Buttery smooth rounded contours, soft drooping curvature, zero sharp edges, zero visible polygon facets. Soft pastel mint green and mellow lime gradients, smooth warm brown bark, roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the weeping tree prop, ignore all environment, water, and scenery. Seamless solid pure white background with generous empty padding on all sides. Full view from top arch to drooping foliage tips and bottom roots, nothing cropped. Clear empty space underneath, strictly floating, no ground, no pond, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, poetic tranquil mood, tactile finish --ar 1:1
  ```
  *Thông số:* ~350–450 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🌿 Variant B3 (Wild Berry Hazel Bush - Cây Phỉ Nhánh Tròn Trĩu Quả Mọng)**
  ```text
  A 3D video game item asset of a charming stylized shrubby woodland hazel tree, centered in frame, floating weightlessly in mid-air in empty space. Multi-stemmed organic trunk branching low into four smooth curved limbs that cradle a plump, compact rounded dome of buttery smooth cloud puff foliage. Adorned with several oversized, cute rounded wild red forest berries nestled visibly among the foliage puffs like bright colorful jewels. Buttery smooth marshmallow-like volumes, zero sharp edges, zero visible polygon facets. Lush sage green foliage with vibrant coral-red berry accents, warm chestnut branches, stems ending cleanly in mid-air. Isometric 3/4 perspective view. Focus only on the berry tree prop, ignore all forest scenery and ground. Seamless solid pure white background with generous padding on all sides. Full view, nothing cropped or touching the frame borders. Clear empty air underneath, strictly floating, no soil, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, cheerful bountiful nature accent --ar 1:1
  ```
  *Thông số:* ~320–420 tris | *Pivot:* Đáy gốc tiếp đất (Y=0)

* **🌿 Variant C (Twin Young Birch - Cặp Bạch Dương Song Sinh Dẻo Dai)**
  ```text
  A 3D video game item asset of a pair of stylized organic young birch trees, designed for subtle wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Two slender pale warm-gray trunks branching upward in a graceful springy V-shape, topped with small separate spherical cloud-like lime-green canopies fluttering at the tips. Buttery smooth rounded volumes, soft curvature, zero sharp edges, zero visible polygon facets. Clear air gap between the twin trunks, light and lively spring feel, roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus only on the birch trees, ignore all environment and scenery. Seamless solid pure white background with generous padding around all sides. Full view from top to bottom, nothing cropped. Clear empty air underneath, strictly floating, no ground, no pedestal, no base. In the distinctive art style of Preserve puzzle game: chunky soft rounded diorama, clean pastel gradients --ar 1:1
  ```
  *Thông số:* ~350–450 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🌿 Variant C2 (Solitary S-Curve White Birch - Bạch Dương Đơn Thân Uốn Lượn)**
  ```text
  A 3D video game item asset of a slender minimalist stylized white birch tree, designed for springy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. A single graceful chalk-white trunk with gentle organic S-curve curvature and subtle warm charcoal bark accents, holding two small separate spherical cloud-like foliage puffs near the tapering crown. High visual lightness and airy negative space around the slender trunk, creating a springy elegant silhouette, root base ending cleanly in mid-air. Buttery smooth rounded volumes, soft curvature, zero sharp edges, zero visible polygon facets. Vibrant fresh golden-lime and sunlit chartreuse gradients. Isometric 3/4 perspective view. Focus exclusively on the solitary birch, ignore all background and landscape. Seamless solid pure white background with generous padding around all sides. Full view from tip to base, nothing cropped. Clear empty air underneath, strictly floating, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded diorama, clean pastel freshness --ar 1:1
  ```
  *Thông số:* ~260–350 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🍀 Variant D (Undergrowth Fern Bush - Bụi Dương Xỉ Xòe Lớp Mềm Mượt)**
  ```text
  A 3D video game item asset of a stylized forest undergrowth fern bush, centered in frame, floating weightlessly in mid-air in empty space. Broad chunky rounded fern fronds radiating and unfurling outward in natural overlapping tiers around a compact central foliage rosette, rich emerald and pastel mint green. Buttery smooth beveled leaf curves, soft organic contours, zero sharp polygonal facets, frond tips drooping gracefully with gravity, bottom base ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the bush prop, ignore all forest floor, scenery, and environment. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped or touching the image border. Clear empty space underneath, strictly floating, no ground, no dirt, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, smooth tactile aesthetic --ar 1:1
  ```
  *Thông số:* ~180–240 tris | *Pivot:* Đáy tâm bụi cây (Y=0)

* **🍀 Variant E (Hollow Mossy Log - Thân Gỗ Mục Vững Chãi Đính Nấm)**
  ```text
  A 3D video game item asset of a stylized fallen hollow tree log, centered in frame, floating weightlessly in mid-air in empty space. Solid grounded presence with thick rounded bark and a charming open hollow center, decorated with plush buttery smooth emerald velvet moss cushions and two tiny pastel button mushrooms sprouting organically on top. Zero sharp faceted edges, soft chamfered wooden contours, bottom surface floating cleanly in air. Isometric 3/4 perspective view. Focus only on the log prop, ignore all terrain, ground, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the edges. Clear empty air below, strictly floating, no soil, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, warm pastel colors, cozy woodland feel --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy tiếp đất (Y=0)

* **🍀 Variant F (Mossy Sprout Stump - Gốc Cây Cổ Thụ Mục Điểm Chồi Non & Nấm Rừng)**
  ```text
  A 3D video game item asset of a stylized ancient mossy tree stump, centered in frame, floating weightlessly in mid-air in empty space. A compact, stout, weathered tree stump with thick wrinkled bark and wide flared root toes, topped with a flat cut surface covered in a plush buttery smooth emerald velvet moss cushion. A tender young sapling with two cute round lime-green leaves sprouts cheerfully from a crevice, accompanied by two tiny warm-coral button mushrooms growing at the root base. Buttery smooth rounded volumes, soft organic contours, zero sharp polygonal facets, roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the tree stump prop, ignore all forest floor, ground, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view from top to bottom roots, nothing cropped or touching the image border. Clear empty space underneath, strictly floating, no ground, no dirt mound, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, cozy woodland feel, tactile finish --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🍀 Variant G (Enchanted Woodland Toadstools - Cụm Nấm Rừng Đỏ Chấm Bi Múp Míp)**
  ```text
  A 3D video game item asset of a charming cluster of three stylized woodland toadstool mushrooms, centered in frame, floating weightlessly in mid-air in empty space. Three plump rounded mushrooms clustered snugly together at staggered heights, featuring chunky domed caps in vibrant warm vermilion-red with soft raised buttery cream-white polka dots, supported by thick curved pale cream-ivory stalks with cozy little neck ruffles. A tiny patch of plush velvety moss nestles at the base of the stalks, ending cleanly in mid-air. Buttery smooth marshmallow-like surfaces, soft organic curvature, zero sharp edges, zero visible polygon facets. Isometric 3/4 perspective view. Focus only on the mushroom cluster, ignore all forest floor, scenery, and environment. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no dirt, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, cute whimsical fairy-tale forest accent, vibrant warm contrast --ar 1:1
  ```
  *Thông số:* ~180–240 tris | *Pivot:* Đáy tâm cụm (Y=0)

* **🍀 Variant H (Mossy Forest Boulders - Cụm Đá Cuội Rừng Phủ Đệm Rêu Êm)**
  ```text
  A 3D video game item asset of a natural cluster of stylized forest river stones and pebbles, centered in frame, floating together weightlessly in mid-air in empty space. Two smooth, rounded weathered river stones nestled snugly side-by-side with one tiny companion pebble, topped with soft buttery smooth sage-green velvet moss caps and a single tiny curly fern sprout unfurling from the seam. Solid grounded stone presence with gentle chamfered beveled contours, zero sharp jagged ridges, bottom surfaces floating cleanly in mid-air. Warm slate-gray and soft emerald gradients. Isometric 3/4 perspective view. Focus exclusively on the stones and moss prop, ignore all terrain, mud, and background landscape. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no soil, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded diorama, calm natural grounding scatter --ar 1:1
  ```
  *Thông số:* ~160–220 tris | *Pivot:* Đáy tiếp đất (Y=0)

* **🍀 Variant I (Shaded Forest Bluebells - Khóm Hoa Chuông Rừng Dại Uốn Mềm)**
  ```text
  A 3D video game item asset of a delicate cluster of stylized shaded woodland bluebells, centered in frame, floating weightlessly in mid-air in empty space. A compact low-lying clump of three graceful pliant arching green stems bearing four nodding bell-shaped blossom cups in pastel periwinkle-blue and gentle lavender gradients, nestled above a few broad, buttery smooth rounded forest floor leaves. Soft volumetric petal bells drooping gracefully with gravity, zero sharp polygonal edges, zero visible facets, stem base ending cleanly in air. Isometric 3/4 perspective view. Focus only on the wildflower cluster, ignore all forest floor, ground, and scenery. Seamless solid pure white background with generous empty padding on all sides. Full view from blossom tips to stem base, nothing cropped. Clear empty air underneath, strictly floating, no dirt mound, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, elegant whimsical woodland scatter --ar 1:1
  ```
  *Thông số:* ~180–250 tris | *Pivot:* Đáy gốc cành (Y=0)

* **🌱 Variant J (Micro Soft Grass Tuft - Chùm Cỏ Múp Míp Tí Hon Lấp Khe Trống)**
  ```text
  A 3D video game item asset of a tiny minimalist cluster of stylized micro grass tufts, ground scatter filler, centered in frame, floating weightlessly in mid-air in empty space. A compact low-lying sprout of three short, plump, buttery smooth rounded grass blades gently curving outward from a tiny central root point, soft marshmallow-like volumes, zero sharp faceted edges. Vibrant sunlit lime green and fresh pastel chartreuse gradients, bottom stem base ending cleanly in air. Isometric 3/4 perspective view. Focus only on the tiny grass tuft, ignore all ground, dirt, and background. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air underneath, strictly floating, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, cute minimalist micro ground detail --ar 1:1
  ```
  *Thông số:* ~60–90 tris | *Pivot:* Đáy gốc (Y=0)

* **🌱 Variant K (Plush Moss Mound Patch - Mảng Đệm Rêu Phồng Mềm Mại Sát Đất)**
  ```text
  A 3D video game item asset of a small stylized velvety moss cushion mound, ground surface filler, centered in frame, floating weightlessly in mid-air in empty space. A gentle, low, flattened undulating mound of buttery smooth cloud-like moss volume, soft puffed organic contours, zero sharp polygonal facets, designed to sit directly on the ground surface. Rich emerald green blending into soft sage moss gradients, completely flat bottom ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the moss mound asset, ignore all terrain, mud, and background. Seamless solid pure white background with generous padding on all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no ground plane, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, plush velvet tactile feel --ar 1:1
  ```
  *Thông số:* ~80–120 tris | *Pivot:* Đáy tiếp xúc phẳng (Y=0)

* **🌱 Variant L (Micro Clover Tuft - Chùm Cỏ Ba Lá Tí Hon Sát Đất)**
  ```text
  A 3D video game item asset of a tiny cluster of stylized miniature clover leaves, ground scatter filler, centered in frame, floating weightlessly in mid-air in empty space. Four small, chunky, heart-shaped clover leaflets on tiny curved tender green stems clustered snugly together just above ground level. Buttery smooth rounded leaf shapes, soft organic contours, zero sharp edges, zero visible polygon facets. Fresh vivid emerald and pastel mint tones, stem bases cleanly joined in mid-air. Isometric 3/4 perspective view. Focus only on the clover leaves, ignore all soil, meadow, and background. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty space underneath, strictly floating, no dirt, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, cute fairytale ground accent --ar 1:1
  ```
  *Thông số:* ~80–110 tris | *Pivot:* Đáy gốc cành (Y=0)

* **🌱 Variant M (Fallen Oak Leaves & Acorn Scatter - Vụn Lá Sồi Vàng & Quả Sồi Rơi)**
  ```text
  A 3D video game item asset of two stylized fallen autumn oak leaves and a single tiny round acorn resting together, forest floor micro scatter, centered in frame, floating weightlessly in mid-air in empty space. Two chunky rounded leaves with buttery smooth scalloped edges resting flatly overlapping, colored in warm amber orange and sunlit golden honey gradients, paired with a cute plump little chestnut-brown acorn with a textured cap. Zero sharp faceted edges, soft chamfered contours, flat bottoms ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the leaves and acorn prop, ignore all forest floor, ground, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded diorama, cozy woodland ground detail, warm color pop --ar 1:1
  ```
  *Thông số:* ~100–140 tris | *Pivot:* Đáy tiếp xúc phẳng (Y=0)

* **🌱 Variant N (Flat River Pebble Trio - Cụm 3 Viên Sỏi Cuội Bẹp Mini)**
  ```text
  A 3D video game item asset of three tiny smooth rounded river pebbles nestled flatly together, micro ground filler, centered in frame, floating weightlessly in mid-air in empty space. Three small, flattened, buttery smooth pebble stones of varying mini sizes, one stone adorned with a tiny patch of soft velvety green moss. Gentle rounded beveled contours, zero sharp edges, zero jagged ridges, flat bottom surfaces floating cleanly in air. Warm slate-gray and soft earth tones. Isometric 3/4 perspective view. Focus only on the pebble stones, ignore all ground and background. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty air underneath, strictly floating, no dirt, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, subtle natural ground grounding --ar 1:1
  ```
  *Thông số:* ~70–100 tris | *Pivot:* Đáy phẳng tiếp đất (Y=0)

---

### 2. Bloomfield (Đồng Cỏ Thảo Nguyên & Bụi Hoa Rực Rỡ)
* **Bảng màu:** Xanh mạ non (Fresh Chartreuse), Vàng bơ (Buttercup Yellow), San hô pastel (Pastel Coral Pink), Trắng kem.

* **👑 Variant A (Prairie Flower Patch - Cụm Hoa Bông Lau & Cúc Vàng Đại Đóa - HERO FLORAL)**
  ```text
  A 3D video game item asset of a majestic grand wildflower bouquet cluster, colossal meadow hero centerpiece, designed for gentle breeze sway animation, centered in frame, floating weightlessly in mid-air in empty space. A dense, opulent floral dome of pliant slender green stems fanning gracefully outward, bearing numerous (6 to 8) oversized cheerful buttercups with layered buttery smooth rounded petals in golden yellow and coral pink, interwoven with tall fluffy cloud-like foxtail plumes. Rich volumetric floral presence with airy negative space between stems, springy bounce feel, stem bases cleanly bound in mid-air. Buttery smooth organic curves, zero sharp edges, zero visible polygon facets. Vibrant fresh chartreuse and pastel floral gradients. Isometric 3/4 perspective view. Focus exclusively on the grand flower cluster, ignore all landscape, meadow, and background. Seamless solid pure white background with generous empty padding on all sides. Full view from top to bottom, nothing cropped. Clear empty air underneath, strictly floating, no ground, no dirt mound, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, magnificent, airy and lively --ar 1:1
  ```
  *Thông số:* ~450–580 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🍀 Variant B (Clover & Daisy Clump - Cụm Cỏ Ba Lá Điểm Cúc Trắng 3D - PROVEN)**
  ```text
  A 3D video game item asset of a compact cluster of stylized clover foliage and prominent blooming daisies, centered in frame, floating weightlessly in mid-air in empty space. A lush mound of rich green clover leaves, with numerous (5 to 7) oversized, chunky 3D white daisy blossoms rising distinctly on short sturdy stems above the foliage. Each daisy features plump, opaque, pure snow-white rounded petals and a raised golden-yellow button center, with clear separation between the flowers and the green leaves. Buttery smooth rounded volumes, zero sharp edges, zero visible polygon facets. Isometric 3/4 perspective view. Focus only on the flower cluster, ignore all soil, terrain, and environment. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty space below, strictly floating, no ground mound, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, crisp vibrant colors --ar 1:1
  ```
  *Thông số:* ~400–600 tris | *Pivot:* Đáy tâm cụm (Y=0)

* **🌿 Variant C (Lavender Spike Bush - Bụi Oải Hương Tầng Nhọn Mềm)**
  ```text
  A 3D video game item asset of a compact stylized lavender bush, centered in frame, floating weightlessly in mid-air in empty space. Rounded olive foliage cushion with four slender upright conical lavender flower spikes staggered at naturally varying heights, pastel lilac and lavender tones. Soft cloud-like bud clusters, buttery smooth curvature, zero sharp polygonal edges, bottom ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the bush prop, ignore all background scenery, meadow, and ground. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing touching the image borders. Clear empty air underneath, strictly floating, no ground, no pedestal, no base. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, clean shading --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy gốc (Y=0)

* **🌿 Variant D (Giant Whimsical Bellflower - Bông Hoa Chuông Uốn Cong Mọng Nước)**
  ```text
  A 3D video game item asset of a single oversized whimsical stylized bellflower, centered in frame, floating weightlessly in mid-air in empty space. Elegant slender green stem arching gracefully under the gentle weight of a large plump bell blossom in gradient peach-pink, with a single stylized rounded leaf branching sideways. Buttery smooth curving petals, soft volumetric shape, zero sharp edges, zero visible polygon facets, stem bottom ending cleanly in air. Isometric 3/4 perspective view. Focus only on the flower, ignore all environment, scenery, and ground. Seamless solid pure white background with generous empty padding on all sides. Full view from top to bottom, nothing cropped or touching the frame edges. Clear empty space below, strictly floating, no dirt, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, cute exaggerated proportions, springy bounce feel --ar 1:1
  ```
  *Thông số:* ~160–220 tris | *Pivot:* Gốc cành tiếp đất (Y=0)

* **🍀 Variant E (Pebble Flower Scatter - Cặp Sỏi Cuội Mịn Màng Nở Hoa Nhỏ)**
  ```text
  A 3D video game item asset of two smooth rounded gray pebble stones nestled together with 3 delicate yellow flower buds sprouting from their crevice, centered in frame, floating together weightlessly in mid-air in empty space. Solid weighty stone presence with buttery smooth rounded chamfers, zero sharp faceted edges, contrasting with the soft lively flower petals, flat bottom floating cleanly. Isometric 3/4 perspective view. Focus only on the stones and flower buds, ignore all ground, dirt, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no soil, no ground mound, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded diorama, minimalist cozy scatter --ar 1:1
  ```
  *Thông số:* ~140–200 tris | *Pivot:* Đáy tâm (Y=0)

---

### 3. Windheath (Đồi Thạch Nam & Gió Lộng Cao Nguyên)
* **Bảng màu:** Tím thạch nam (Heather Violet/Magenta), Xanh rêu xám (Sage Green), Xám đá ấm (Warm Granite Slate).

* **👑 Variant A (Windswept Gnarled Pine - Cổ Thụ Thông Núi Gió Uốn - HIGHLAND HERO TREE)**
  ```text
  A 3D video game item asset of a majestic ancient windswept mountain dwarf pine, colossal highland hero tree centerpiece, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Powerful, thick, gnarled wooden trunk twisted dramatically by highland gales, with wide muscular root toes gripping the air, heavy horizontal boughs branching into a grand tiered cloud-like canopy of numerous (5 to 6) broad horizontal needle pads in sage green with deep negative space between tiers. Rugged ancient presence with buttery smooth rounded bark contours and soft cloud curvature, zero sharp edges, zero visible polygon facets, trunk base ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the colossal pine, ignore all mountain scenery, terrain, and background. Seamless solid pure white background with generous empty padding around all sides. Full view from top to bottom, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground, no rock platform, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, magnificent, ancient, dynamic highland character --ar 1:1
  ```
  *Thông số:* ~500–650 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **🌿 Variant B (Heather Shrub Clump - Khóm Thạch Nam Bồng Bềnh)**
  ```text
  A 3D video game item asset of a dense low-lying stylized heather shrub clump, centered in frame, floating weightlessly in mid-air in empty space. Organic puffy volume with soft cloud curvature, vibrant gradient of magenta and pastel lavender flower puffs clustering over deep olive foliage. Buttery smooth rounded volumes, zero sharp edges, zero visible polygon facets, bottom ending cleanly in air. Isometric 3/4 perspective view. Focus only on the shrub clump, ignore all moorland landscape, terrain, and background. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty space below, strictly floating, no ground, no terrain base, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, tactile feel --ar 1:1
  ```
  *Thông số:* ~250–320 tris | *Pivot:* Đáy khóm (Y=0)

* **🍀 Variant C (Upright Tussock Grass - Bụi Cỏ Đồi Dáng Đứng Thẳng Tự Nhiên - RESTING STATE)**
  ```text
  A 3D video game item asset of a compact cluster of stylized upright tussock grass stalks, designed for dynamic wind wave shader animation, centered in frame, floating weightlessly in mid-air in empty space. Chunky rounded grass blades growing straight up in a clean vertical sheaf posture, perfectly upright and standing tall in calm still air with zero wind deflection, stems bound neatly at the bottom center and fanning very subtly in radial symmetry without any directional lean. Pale straw ochre and pastel olive tones. Buttery smooth rounded blades, zero sharp faceted edges, bottom stems ending cleanly in mid-air at bottom pivot Y=0. Isometric 3/4 perspective view. Focus exclusively on the grass stalks, ignore all environment, ground, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the borders. Clear empty air underneath, strictly floating, no soil, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean shading, neutral upright resting pose --ar 1:1
  ```
  *Thông số:* ~180–240 tris | *Pivot:* Đáy cụm cỏ (Y=0)

* **👑 Variant D (Highland Boulder Slabs - Khối Đá Tảng Tựa Khối Vững Chãi - ROCK HERO)**
  ```text
  A 3D video game item asset of a majestic granite rock formation, solid centerpiece rock prop, centered in frame, floating together weightlessly in mid-air in empty space. Three massive weathered smooth granite boulder slabs leaning stably against each other, topped with plush flat pastel sage-green lichen caps. Solid monumental stone presence with clean rounded beveled chamfers, zero sharp jagged ridges, bottoms of the stones floating cleanly. Isometric 3/4 perspective view. Focus only on the rock formation, ignore all mountain terrain, landscape, and environment. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air underneath, strictly floating, no ground, no pedestal, no base. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, weighted diorama asset, ancient stability --ar 1:1
  ```
  *Thông số:* ~300–400 tris | *Pivot:* Đáy khối đá (Y=0)

* **🌿 Variant E (Slender Dwarf Birch - Bạch Dương Cằn Cỗi Nhẹ Tênh)**
  ```text
  A 3D video game item asset of a minimalist stylized dwarf birch tree, designed for delicate wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Slender pale chalk-white trunk with subtle organic crooked bends, sparse golden-yellow cloud-like foliage puffs fluttering lightly at the branch tips. Buttery smooth rounded canopies, zero sharp polygonal edges, trunk bottom ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the birch tree, ignore all autumn landscape, ground, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view from top to bottom, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, airy windswept aesthetic --ar 1:1
  ```
  *Thông số:* ~280–380 tris | *Pivot:* Đáy gốc cây (Y=0)

---

# 💧 PHẦN 2: WET ISLAND

---

### 1. Reedmarsh (Đầm Sậy & Bãi Sình Lầy)
* **Bảng màu:** Nâu đầu sậy (Cattail Ochre), Xanh ô liu ẩm (Marsh Olive), Nâu đất sình ấm (Warm Mud Brown).

* **👑 Variant A (Tall Cattail Clump - Cụm Sậy Nước Khổng Lồ Tâm Điểm - HERO CLUMP)**
  ```text
  A 3D video game item asset of a majestic grand cattail reed cluster, colossal wetland centerpiece prop, designed for gentle reed sway animation, centered in frame, floating weightlessly in mid-air in empty space. A dense, powerful cluster of 7 to 9 tall upright green stalks bearing thick tubular buttery smooth velvet-brown seed heads at staggered heights, framed by broad arching marsh grass blades fanning outward in a grand fountain-like dome, bound neatly at the bottom with stem ends ending cleanly in mid-air. Buttery smooth rounded seed heads and curving blades, zero sharp faceted edges. Isometric 3/4 perspective view. Focus only on the cattail reeds, ignore all marsh, water, mud, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view from top to bottom, nothing cropped or touching the edges. Clear empty space below, strictly floating, no mud mound, no ground, no water plane, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean beveled geometry, springy vertical rhythm, magnificent marsh centerpiece --ar 1:1
  ```
  *Thông số:* ~380–500 tris | *Pivot:* Đáy gốc sậy (Y=0)

* **🍀 Variant B (Swamp Sedge Tuft - Bụi Cỏ Lác Vòng Cung Mềm)**
  ```text
  A 3D video game item asset of a compact stylized tuft of swamp sedge grass, centered in frame, floating weightlessly in mid-air in empty space. Chunky curved blades radiating outward in a natural fountain-like arc from a central base, deep mossy green with pale chartreuse tips drooping slightly with gravity. Buttery smooth blade curvature, zero sharp polygonal edges, bottom ending cleanly in mid-air. Isometric 3/4 perspective view. Focus only on the sedge grass tuft, ignore all wetland environment, mud, and scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air underneath, strictly floating, no ground, no pedestal, no base. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, organic fountain shape --ar 1:1
  ```
  *Thông số:* ~180–250 tris | *Pivot:* Đáy cụm (Y=0)

* **👑 Variant C (Ancient Weeping Willow - Đại Thụ Liễu Đầm Lầy Rủ Tầng Mây - WETLAND HERO TREE)**
  ```text
  A 3D video game item asset of a majestic ancient grand weeping willow tree, colossal wetland hero centerpiece, designed for flowing wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Swollen, massive, gnarled ancient trunk with wide flared buttress roots gripping into mid-air, branching outward into heavy arching boughs that drape numerous (6 to 8) cascading tiered cloud-like weeping foliage veils in fresh pale spring green. Generous negative space between limbs and foliage curtains, flowing gracefully with gravity. Buttery smooth rounded cloud curvature, zero sharp polygonal edges, bottom roots ending cleanly in mid-air. Isometric 3/4 perspective view. Focus exclusively on the colossal willow tree, ignore all water, pond, river, and landscape. Seamless solid pure white background with generous empty padding around all sides. Full view from crown to bottom roots, nothing cropped or touching the frame edges. Clear empty space underneath, strictly floating, no ground, no water plane, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, magnificent, ancient, flowing and graceful --ar 1:1
  ```
  *Thông số:* ~550–700 tris | *Pivot:* Đáy gốc cây (Y=0)

* **🍀 Variant D (Waterlogged Stump - Gốc Cây Mục Ngâm Nước Chắc Nịch)**
  ```text
  A 3D video game item asset of a stylized weathered tree stump, centered in frame, floating weightlessly in mid-air in empty space. Solid weighted presence, flat cut top surface displaying subtle concentric growth rings, smooth rounded bark adorned with two stepped bracket shelf mushrooms and plush buttery smooth emerald moss cushions, bottom of the stump ending cleanly in air. Zero sharp jagged edges. Isometric 3/4 perspective view. Focus only on the stump prop, ignore all swamp, mud, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no ground soil, no water, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, cozy forest scatter --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy thân gỗ (Y=0)

* **🍀 Variant E (Marsh Reed Shoots - Cụm Chồi Sậy Non Thon Gọn Mịn)**
  ```text
  A 3D video game item asset of a delicate pair of stylized green water reed shoots accompanied by two tiny oval marsh leaves, centered in frame, floating weightlessly in mid-air in empty space. Slender organic shoots with buttery smooth beveled tips, zero sharp polygonal edges, bottoms ending cleanly in air, fresh minimalist marsh flora. Isometric 3/4 perspective view. Focus only on the reed shoots, ignore all marsh mud, water, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the frame edges. Clear empty space below, strictly floating, no mud island, no water surface, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, minimalist plant asset --ar 1:1
  ```
  *Thông số:* ~140–180 tris | *Pivot:* Đáy cụm chồi (Y=0)

---

### 2. Lilywater (Hồ Sen Súng & Vườn Nước Thanh Tịnh)
* **Bảng màu:** Xanh ngọc lá sen (Emerald Green), Hồng cánh sen chuyển sắc (Pastel Lotus Pink), Vàng nhị hoa tươi.

* **👑 Variant A (Royal Water Lily Raft with Grand Lotus - Cụm Bè Sen Đại Đóa Nở Rộ - AQUATIC HERO)**
  ```text
  A 3D video game item asset of a majestic grand water lily centerpiece cluster, serene aquatic hero prop, centered in frame, floating weightlessly in mid-air in empty space. A lush floating raft of four large overlapping circular emerald-green lilypads with clean V-notches resting flat horizontally, crowned with an opulent, oversized blooming lotus flower featuring multiple layers of buttery smooth petals cupping gracefully upward in soft gradient pastel pink around a glowing golden center. Beautiful aquatic symmetry with generous floating presence, zero sharp faceted edges, zero visible polygon ridges, bottom surfaces floating cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the grand lotus cluster, ignore all pond water, lake, ripple, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no water plane, no pool base, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, magnificent, serene aquatic floral centerpiece --ar 1:1
  ```
  *Thông số:* ~400–550 tris | *Pivot:* Đáy phẳng cụm sen (Y=0)

* **🍀 Variant B (Dual Overlapping Lilypads - Cặp Lá Sen Nổi Xếp So Le)**
  ```text
  A 3D video game item asset of two overlapping stylized round lilypads of different sizes resting flat horizontally, centered in frame, floating weightlessly in mid-air in empty space. Chunky rounded rim, vibrant jade green with subtle radial notch cuts, buttery smooth flat surfaces, clean bottom surfaces floating in mid-air. Zero sharp faceted edges. Isometric 3/4 perspective view. Focus only on the two lilypads, ignore all water, pond, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no water pool, no base disc, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean minimalist aquatic asset --ar 1:1
  ```
  *Thông số:* ~140–190 tris | *Pivot:* Đáy phẳng lá sen (Y=0)

* **🌿 Variant C (Emerging Lotus Bud - Búp Sen Vươn Cuống Cong Mọng)**
  ```text
  A 3D video game item asset of a single stylized lotus flower bud on a gracefully curved green stem, centered in frame, floating weightlessly in mid-air in empty space. Plump rounded bud with tightly wrapped petals tipping in gradient soft pink, accompanied by one small round lilypad at the stem base, bottom ending cleanly in air. Buttery smooth organic curvature, zero sharp polygonal edges. Isometric 3/4 perspective view. Focus only on the lotus bud and stem, ignore all water, pond surface, and background. Seamless solid pure white background with generous empty padding around all sides. Full view from top to bottom, nothing cropped or touching the edges. Clear empty space below, strictly floating, no water plane, no base stand, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, Zen aquatic flower asset, springy stem curve --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy cuống sen (Y=0)

* **🌿 Variant D (Water Iris Cluster - Cụm Diên Vĩ Lá Kiếm & Hoa Nở Tầng Mềm)**
  ```text
  A 3D video game item asset of a stylized Japanese water iris plant clump, centered in frame, floating weightlessly in mid-air in empty space. Upright slender sword-like green leaves fanning gracefully, bearing three elegant blossoms in royal purple and pastel violet with yellow throat accents. Buttery smooth petal folds, zero sharp origami facets, leaf bases ending cleanly in air. Isometric 3/4 perspective view. Focus only on the iris flowers and leaves, ignore all water, riverbank, and landscape. Seamless solid pure white background with generous empty padding on all sides. Full view from top to bottom, nothing cropped. Clear empty air underneath, strictly floating, no water base, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean shading, elegant floral silhouette --ar 1:1
  ```
  *Thông số:* ~320–420 tris | *Pivot:* Đáy gốc hoa (Y=0)

* **🍀 Variant E (Duckweed Leaflet Cluster - Mảng Bèo Tấm Nổi Xếp Cụm Nhỏ)**
  ```text
  A 3D video game item asset of five tiny stylized floating duckweed discs clustered naturally together, centered in frame, floating weightlessly in mid-air in empty space. Soft pastel green rounded oval pads with smooth beveled rims resting flat horizontally, buttery smooth surfaces, zero sharp edges, bottom floating cleanly in air. Isometric 3/4 perspective view. Focus only on the duckweed discs, ignore all water ripples, pond, and scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing touching the image borders. Clear empty space below, strictly floating, no water plane, no base disc, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, subtle water scatter asset --ar 1:1
  ```
  *Thông số:* ~100–150 tris | *Pivot:* Đáy mảng bèo (Y=0)

---

### 3. Streamstone (Suối Đá Rêu & Ghềnh Thác Suối)
* **Bảng màu:** Xám đá cuội mịn (Smooth Slate Gray), Rêu nhung lục bảo (Velvet Emerald Moss), Xanh ngọc nước mát.

* **👑 Variant A (Stepped Mossy Boulders - Ghềnh Đá Xếp Bậc Đại Khối - ROCK HERO)**
  ```text
  A 3D video game item asset of a majestic stepped granite river boulder formation, solid centerpiece rock prop, centered in frame, floating together weightlessly in mid-air in empty space. Three massive rounded granite boulders resting stably in a natural stepped tier, each stone capped with a plush, voluminous, buttery smooth puffy cushion of emerald green velvet moss. Solid weighted stone presence with soft chamfered edges, zero sharp jagged ridges, bottoms of the stones floating cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the grand stepped boulders and moss, ignore all river, water, streambed, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground, no riverbed, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean rounded geometry, monumental weighted presence --ar 1:1
  ```
  *Thông số:* ~380–500 tris | *Pivot:* Đáy khối đá (Y=0)

* **👑 Variant B (Ancient Root-Over-Rock - Đại Cổ Thụ Siết Đá Suối - NATURE MONUMENT)**
  ```text
  A 3D video game item asset of a majestic ancient root-over-rock monument, colossal nature centerpiece prop, centered in frame, floating weightlessly in mid-air in empty space. Massive, muscular ancient tree roots with thick organic gnarled bark wrapping powerfully around a colossal smooth rounded granite river boulder in a dramatic eternal embrace. Solid heavy stone presence with clean rounded beveled chamfers, complemented by plush buttery smooth emerald moss cushions nestled in the root crevices, bottoms floating cleanly in air. Strong interplay of ancient wood and permanent stone, zero sharp edges, zero visible polygon facets. Isometric 3/4 perspective view. Focus only on the root and boulder monument, ignore all riverbed, soil, water, and scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped. Clear empty air below, strictly floating, no ground, no river, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, clean topology, dynamic monumental interplay --ar 1:1
  ```
  *Thông số:* ~450–600 tris | *Pivot:* Đáy khối đá (Y=0)

* **🍀 Variant C (Trio of River Pebbles - Ba Viên Sỏi Sông Tròn Mịn)**
  ```text
  A 3D video game item asset of three smooth water-worn river pebbles of different sizes nestled together with a tiny plush green moss cushion in the crevice, centered in frame, floating together weightlessly in mid-air in empty space. Warm gray slate color with buttery soft rounded contours, solid permanent stone feel, zero sharp ridges, bottom floating cleanly. Isometric 3/4 perspective view. Focus only on the three pebbles, ignore all riverbed, ground, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the edges. Clear empty space below, strictly floating, no ground, no dirt, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, minimalist cozy scatter --ar 1:1
  ```
  *Thông số:* ~120–170 tris | *Pivot:* Đáy cụm sỏi (Y=0)

* **🌿 Variant D (Stream-Bank Water Fern - Bụi Dương Xỉ Nước Tán Xòe Dẻo Dai)**
  ```text
  A 3D video game item asset of a lush stylized water fern clump fanning gracefully outward from a tiny smooth pebble anchor, centered in frame, floating weightlessly in mid-air in empty space. Chunky emerald green fronds with gentle springy curves, buttery smooth rounded leaf planes, zero sharp polygonal facets, bottom ending cleanly in air. Isometric 3/4 perspective view. Focus only on the fern clump, ignore all riverbank, mud, water, and scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, stream-bank vegetation asset --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy gốc (Y=0)

* **🌿 Variant E (Stone Slab Crossing - Cầu Đá Tự Nhiên Chắc Chắn)**
  ```text
  A 3D video game item asset of a rustic stylized stone stepping bridge, two thick rounded moss-capped boulder pillars supporting a smooth flat stone stepping slab, centered in frame, floating weightlessly in mid-air in empty space. Solid architectural stability with soft toy-like rounded beveled edges, zero sharp jagged ridges, bottom of the pillars floating cleanly in air. Isometric 3/4 perspective view. Focus only on the stone bridge structure, ignore all river, water, riverbed, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped. Clear empty space below, strictly floating, no riverbed, no water, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, toy-like tactile asset, weighted stability --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy cột đá (Y=0)

---

### 4. Rainleaf (Rừng Mưa Nhiệt Đới & Tán Lá Khổng Lồ)
* **Bảng màu:** Xanh rừng thẳm (Deep Jungle Green), Đỏ cam chuối pháo (Fiery Heliconia Scarlet/Orange), Nâu vỏ cây nhiệt đới.

* **👑 Variant A (Giant Emergent Buttress Tree - Đại Thụ Rừng Mưa Rễ Bạnh Khổng Lồ - TROPICAL HERO COLOSSUS)**
  ```text
  A 3D video game item asset of a colossal ancient emergent rainforest tree, grand tropical hero tree centerpiece, designed for gentle canopy wind sway animation, centered in frame, floating weightlessly in mid-air in empty space. Massive towering trunk anchored by dramatic, wide-flaring triangular buttress root fins that spread powerfully at the base, dividing high up into heavy horizontal boughs that support an expansive, multi-tiered canopy of numerous (8 to 10) sprawling umbrella-shaped cloud-like foliage decks in rich deep jungle green with generous negative space between tiers. Majestic architectural presence, buttery smooth rounded foliage volumes, zero sharp polygon facets, root bottoms ending cleanly in air. Isometric 3/4 perspective view. Focus exclusively on the colossal rainforest tree, ignore all jungle background, rainforest floor, and landscape. Seamless solid pure white background with generous empty padding around all sides. Full view from crown to bottom roots, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft rounded toy diorama, magnificent tropical colossus, airy and grand --ar 1:1
  ```
  *Thông số:* ~650–850 tris | *Pivot:* Đáy gốc rễ bạnh (Y=0)

* **🌿 Variant B (Broadleaf Palm Tree - Cọ Nhiệt Đới Tán Lá Xòe Cong Mềm Mượt)**
  ```text
  A 3D video game item asset of a stylized tropical palm tree, designed for swaying tropical breeze animation, centered in frame, floating weightlessly in mid-air in empty space. Thick curvy trunk with subtle natural ring segments, crowned with oversized chunky monstera and fan palm fronds arching gracefully outward and drooping slightly under their own weight. Buttery smooth leaf planes with rounded beveled ribs, zero sharp facets, vibrant lime and emerald green gradient, trunk ending cleanly at the bottom in air. Isometric 3/4 perspective view. Focus only on the palm tree, ignore all tropical scenery, ground, and background. Seamless solid pure white background with generous empty padding on all sides. Full view from crown to bottom trunk, nothing cropped or touching the frame edges. Clear empty space below, strictly floating, no ground, no sand, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, mobile game ready 3D asset, flexible tropical silhouette --ar 1:1
  ```
  *Thông số:* ~350–480 tris | *Pivot:* Đáy gốc cây (Y=0)

* **🌿 Variant C (Fiery Heliconia Bush - Bụi Chuối Pháo Trĩu Bông Rực Rỡ)**
  ```text
  A 3D video game item asset of a cluster of stylized hanging Heliconia flowers, centered in frame, floating weightlessly in mid-air in empty space. Slender zigzag stems bearing heavy, dramatic lobster-claw flower bracts in fiery scarlet and golden-yellow gradient, framed by broad chunky tropical banana leaves with soft rounded beveled ribs. Buttery smooth curvature, zero sharp polygonal edges, bottom stems ending cleanly in air. Isometric 3/4 perspective view. Focus only on the heliconia plant, ignore all jungle floor, environment, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the edges. Clear empty air underneath, strictly floating, no ground, no soil, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, vibrant rainforest centerpiece, natural drooping weight --ar 1:1
  ```
  *Thông số:* ~320–440 tris | *Pivot:* Đáy bụi cây (Y=0)

* **🍀 Variant D (Giant Jungle Fern Clump - Cụm Dương Xỉ Nhiệt Đới Xòe Tầng Mềm)**
  ```text
  A 3D video game item asset of a dense low poly cluster of giant tropical jungle fern fronds sprawling horizontally, centered in frame, floating weightlessly in mid-air in empty space. Thick rounded beveled leaf planes with gentle organic outward curve in vibrant gradient green. Buttery smooth surfaces, zero sharp edges, zero visible polygon facets, bottom foliage ending cleanly in air. Isometric 3/4 perspective view. Focus only on the fern clump, ignore all jungle soil, terrain, and background scenery. Seamless solid pure white background with generous empty padding on all sides. Full view, nothing cropped or touching the frame edges. Clear empty space below, strictly floating, no soil, no ground, no pedestal. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, understory plant asset --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy tâm cụm (Y=0)

* **🍀 Variant E (Epiphyte Jungle Branch - Nhánh Cây Rừng Mưa Vương Lan Rừng)**
  ```text
  A 3D video game item asset of a gnarled stylized fallen rainforest branch, centered in frame, floating weightlessly in mid-air in empty space. Natural curved wooden branch form overgrown with cute clinging rounded broadleaf epiphyte orchids and gentle winding vine tendrils, buttery smooth bark contours, bottom surface floating cleanly in air. Isometric 3/4 perspective view. Focus only on the decorated branch, ignore all forest floor, mud, and background scenery. Seamless solid pure white background with generous empty padding around all sides. Full view, nothing cropped or touching the frame edges. Clear empty air underneath, strictly floating, no ground soil, no pedestal, no base. In the distinctive art style of Preserve puzzle game: chunky soft low-poly, cozy ground detail asset --ar 1:1
  ```
  *Thông số:* ~240–320 tris | *Pivot:* Đáy tiếp đất (Y=0)

---

# ⚙️ 4. QUY TRÌNH IMPORT & TỐI ƯU VÀO UNITY (60–120 FPS)

1. **Khi tải file từ AI (Tripo3D, Meshy-4):**
   * Sử dụng công cụ **Retopology / Decimate** trực tiếp trên web của AI:
     * Kéo về **500–700 tris** đối với Hero Trees (Cây Đại Thụ, Liễu Cổ Thụ, Đại Thụ Rễ Bạnh).
     * Kéo về **300–450 tris** đối với Cây tầm trung (Variant B, C).
     * Kéo về **150–250 tris** đối với Bụi cây, Đá, Hoa, Gỗ mục.
   * Xuất định dạng `.FBX` hoặc `.GLB`.

2. **Kiểm tra Pivot Point trong Unity/Blender:**
   * Luôn đảm bảo trục tọa độ của model có đáy nằm tại `Y = 0` để khi `HexHabitatSpawner` đặt lên ô lục giác, cây sẽ chạm đất hoàn hảo mà không bị bay lơ lửng hay chìm sâu.

3. **Bí quyết Color Palette (Tối ưu Draw Calls):**
   * Thay vì dùng 35 ảnh texture 2048x2048 riêng rẽ (tốn RAM và Draw Calls), hãy áp chung **1 Texture bảng màu 256x256 pixel** (Color Palette Texture) cho toàn bộ 35 mô hình.
   * Tạo 1 Material URP/Lit duy nhất, tích chọn **`Enable GPU Instancing`**.
   * Toàn bộ hàng trăm cây trên đảo sẽ được GPU vẽ trong **1 Draw Call duy nhất**, giúp game nhẹ như không!

4. **Tích hợp vào ScriptableObject (`CardData`):**
   * Mở file `CardData` của từng lá bài tương ứng (ví dụ `Card_Leafwood`).
   * Tại danh sách `habitatProps`: Chọn Habitat Type tương ứng và kéo thả cả **5 Prefabs biến thể (Variant A -> E)** vào danh mục `prefabs`.
   * Hệ thống `HexHabitatSpawner` sẽ tự động bốc ngẫu nhiên biến thể, xoay ngẫu nhiên `[-180°, 180°]` và co giãn `[0.85x – 1.15x]` để tạo nên thế giới thiên nhiên sống động và chân thực nhất.
