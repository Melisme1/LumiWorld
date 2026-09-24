# 🌿 LUMIWORLD - BỘ THƯ VIỆN PROMPT 3D CHUẨN ART STYLE PRESERVE

> **Phong cách cốt lõi:** Chunky Soft Low-Poly, Volumetric Puffy Cloud-like Foliage, Smooth Beveled Edges, Toy-like Diorama Proportions, Clean Pastel Gradients, Zero Noise.  
> **Mục tiêu kỹ thuật:** Tối ưu hóa hiệu năng tối đa cho game bàn cờ lục giác (Hex Tile Game), kết hợp cơ chế ngẫu nhiên hóa (Random Variant + Rotation + Scale) trong `HexHabitatSpawner`.

---

## 🚫 1. CẤU HÌNH NEGATIVE PROMPT DÙNG CHUNG (BẮT BUỘC)
*Dán toàn bộ đoạn này vào ô **Negative Prompt** của các AI tạo 3D (Tripo3D, Meshy-4, Sloyd) để loại bỏ hoàn toàn các lỗi vỡ mesh, vân bẩn hay gai nhọn:*

```text
photorealistic, realistic textures, hyperrealistic, noisy bump map, detailed bark grain, alpha cards, transparent leaves, razor-sharp edges, gritty, grunge, dark gothic, high poly sculpt, floating geometry, wireframe, unpolished, text, watermark
```

---

## 🎨 2. MASTER STYLE MODIFIER (TỪ KHÓA ĐẶC TẢ PHONG CÁCH)
*Bất cứ khi nào muốn tạo thêm một vật thể mới ngoài danh sách, chỉ cần ghép đoạn này vào cuối:*

```text
in the distinctive art style of Preserve puzzle game, chunky soft low-poly, volumetric puffy cloud-like foliage, smooth beveled edges, cozy miniature diorama aesthetic, curated warm pastel color palette, soft gradient shading, clean flat surfaces without noisy textures, toy-like tactile feel, cute readable silhouette from isometric top-down view, game-ready 3D asset
```

---

# 🌲 PHẦN 1: STARTER ISLAND

---

### 1. Leafwood (Rừng Sồi Ôn Đới & Tán Lá Bồng Bềnh)
* **Bảng màu:** Xanh ngọc lục bảo nhạt (Sage/Jade Green), Vàng chanh nhẹ ở đỉnh tán, Nâu ấm vỏ cây hạt dẻ (Warm Chestnut Brown).

* **Variant A (Hero Oak - Cây Đại Thụ Tán Mây 2 Tầng)**
  ```text
  A stylized chunky low poly mature oak tree, voluminous puffy cloud-like foliage sculpted in two soft stacked spherical tiers, thick sturdy trunk with smooth rounded roots anchoring into the ground, soft beveled edges, clean silhouette, smooth gradient from warm olive to pastel sunlit green, in the distinctive art style of Preserve puzzle game, cozy tabletop diorama miniature, flat-shaded 3D game asset --ar 1:1
  ```
  *Thông số:* ~450–600 tris | *Pivot:* Đáy tâm gốc cây (Y=0)

* **Variant B (Slender Curved Tree - Cây Dáng Nghiêng Tán Lệch)**
  ```text
  A stylized low poly medium woodland tree, charming slender trunk curving gently sideways, asymmetrical compact foliage clump at the top resembling a soft fluffy marshmallow, warm moss green tones, clean beveled wood, in the distinctive art style of Preserve puzzle game, cozy miniature tabletop aesthetic, mobile game ready --ar 1:1
  ```
  *Thông số:* ~300–400 tris | *Pivot:* Đáy tâm gốc cây (Y=0)

* **Variant C (Twin Young Birch - Cặp Cây Con Song Sinh)**
  ```text
  A pair of stylized low poly young twin birch trees sharing a single rounded base, slender pale warm-gray trunks branching outward, topped with small cute spherical puffy canopies, fresh lime-green pastel foliage, in the distinctive art style of Preserve puzzle game, toy-like tactile feel, clean topology --ar 1:1
  ```
  *Thông số:* ~350–450 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **Variant D (Undergrowth Fern Bush - Bụi Dương Xỉ Đệm Gốc)**
  ```text
  A stylized low poly forest undergrowth bush, chunky thick beveled fern fronds arranged around a soft rounded green foliage ball, rich emerald and pastel mint green, cozy diorama ground vegetation, in the distinctive art style of Preserve puzzle game, clean flat shading --ar 1:1
  ```
  *Thông số:* ~180–240 tris | *Pivot:* Đáy tâm bụi cây (Y=0)

* **Variant E (Hollow Mossy Log - Thân Cây Rỗng Phủ Rêu)**
  ```text
  A stylized low poly hollow fallen tree log, thick smooth bark with beveled edges, open hollow center, decorated with cute puffy velvet-like emerald moss patches and two tiny pastel button mushrooms, in the distinctive art style of Preserve puzzle game, cozy forest floor prop --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy tiếp đất (Y=0)

---

### 2. Bloomfield (Đồng Cỏ Thảo Nguyên & Bụi Hoa Rực Rỡ)
* **Bảng màu:** Xanh mạ non (Fresh Chartreuse), Vàng bơ (Buttercup Yellow), San hô pastel (Pastel Coral Pink), Trắng kem.

* **Variant A (Prairie Flower Patch - Cụm Hoa Bông Lau & Hoa Cúc Cao)**
  ```text
  A clustered patch of stylized low poly tall wildflowers, thick fluffy foxtail plumes and oversized cheerful buttercups on thick smooth stems, soft rounded flower petals in warm yellow and coral pink, resting on a gentle green turf mound, in the distinctive art style of Preserve puzzle game, vibrant cozy diorama asset --ar 1:1
  ```
  *Thông số:* ~350–480 tris | *Pivot:* Đáy tâm gò (Y=0)

* **Variant B (Domed Clover & Daisy Mound - Gò Cỏ Ba Lá & Cúc Trắng)**
  ```text
  A domed puffy low poly grass mound dotted with clusters of chunky stylized white daisies with yellow centers, soft beveled curves, clean pastel gradient green surface, toy-like tactile aesthetic, in the distinctive art style of Preserve puzzle game, tabletop hex prop --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy tâm gò (Y=0)

* **Variant C (Lavender Spike Bush - Bụi Hoa Oải Hương Tím Nhạt)**
  ```text
  A compact stylized low poly bush with four upright conical flower spikes, soft pastel lilac and lavender tones, chunky rounded foliage base, calm floral meadow prop, in the distinctive art style of Preserve puzzle game, clean silhouette, flat-shaded 3D model --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy gốc (Y=0)

* **Variant D (Giant Whimsical Bellflower - Bông Hoa Chuông Khổng Lồ Đơn Lẻ)**
  ```text
  A single oversized whimsical stylized bellflower, thick gently curved green stem supporting a large plump bell-shaped blossom in gradient peach-pink, cute exaggerated diorama proportion, in the distinctive art style of Preserve puzzle game, clean low poly --ar 1:1
  ```
  *Thông số:* ~160–220 tris | *Pivot:* Gốc cành tiếp đất (Y=0)

* **Variant E (Pebble Flower Mound - Mô Đất Đá Cuội Điểm Hoa)**
  ```text
  A subtle stylized low poly earth mound with two smooth rounded gray pebble stones and a tiny cluster of 3 yellow flower buds, smooth beveled planes, minimalist cozy ground scatter, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~140–200 tris | *Pivot:* Đáy tâm (Y=0)

---

### 3. Windheath (Đồi Thạch Nam & Gió Lộng Cao Nguyên)
* **Bảng màu:** Tím thạch nam (Heather Violet/Magenta), Xanh rêu xám (Sage Green), Xám đá ấm (Warm Granite Slate).

* **Variant A (Windswept Gnarled Pine - Cây Thông Cằn Gió Uốn)**
  ```text
  A stylized windswept low poly dwarf mountain pine, thick gnarled wooden trunk bent gracefully to one side, flattened cloud-like needle foliage pads in muted sage green, smooth beveled bark, in the distinctive art style of Preserve puzzle game, highland moor diorama asset, clean topology --ar 1:1
  ```
  *Thông số:* ~400–550 tris | *Pivot:* Đáy tâm gốc (Y=0)

* **Variant B (Heather Carpet Patch - Thảm Bụi Thạch Nam Dày Tím)**
  ```text
  A dense low-lying stylized low poly heather shrub patch, puffy rounded geometric volume, vibrant blended gradient of magenta and pastel lavender flowers over deep olive foliage, in the distinctive art style of Preserve puzzle game, cozy tabletop terrain prop --ar 1:1
  ```
  *Thông số:* ~250–320 tris | *Pivot:* Đáy thảm (Y=0)

* **Variant C (Tussock Grass Clump - Bụi Cỏ Gió Đồi Vàng Khô)**
  ```text
  A cluster of stylized low poly windswept tussock grass, chunky beveled blades leaning uniformly with the wind, pale straw ochre and soft olive tones, highland ground vegetation, in the distinctive art style of Preserve puzzle game, clean low poly 3D model --ar 1:1
  ```
  *Thông số:* ~180–240 tris | *Pivot:* Đáy cụm cỏ (Y=0)

* **Variant D (Highland Boulder Slabs - Khối Đá Tảng Đồi Gió)**
  ```text
  A stylized low poly rock formation consisting of three weathered smooth granite boulder slabs leaning together, topped with flat pastel sage-green lichen caps, smooth chamfered edges, in the distinctive art style of Preserve puzzle game, cozy miniature diorama prop --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy khối đá (Y=0)

* **Variant E (Slender Dwarf Birch - Cây Bạch Dương Cằn Cỗi)**
  ```text
  A minimalist stylized low poly dwarf birch tree, slender pale chalk-white trunk with subtle dark notches, sparse delicate golden-yellow foliage puffs at the crown, windswept autumn aesthetic, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~280–380 tris | *Pivot:* Đáy gốc cây (Y=0)

---

# 💧 PHẦN 2: WET ISLAND

---

### 1. Reedmarsh (Đầm Sậy & Bãi Sình Lầy)
* **Bảng màu:** Nâu đầu sậy (Cattail Ochre), Xanh ô liu ẩm (Marsh Olive), Nâu đất sình ấm (Warm Mud Brown).

* **Variant A (Tall Cattail Clump - Cụm Sậy Nước Thân Nâu Mập)**
  ```text
  A stylized low poly tall cattail reed cluster, thick tubular smooth brown seed heads on upright green stems with curved broad marsh grass blades, rooted in a small rounded mud mound base, in the distinctive art style of Preserve puzzle game, wetland diorama prop, clean beveled geometry --ar 1:1
  ```
  *Thông số:* ~300–420 tris | *Pivot:* Đáy ụ bùn (Y=0)

* **Variant B (Swamp Sedge Tuft - Bụi Cỏ Lác Đầm Lầy)**
  ```text
  A compact stylized low poly tuft of swamp sedge grass, chunky curved blades fanning outwards from a central point, deep mossy green with pale chartreuse tips, clean flat-shaded surfaces, in the distinctive art style of Preserve puzzle game, wetland environment asset --ar 1:1
  ```
  *Thông số:* ~180–250 tris | *Pivot:* Đáy cụm (Y=0)

* **Variant C (Mini Weeping Willow - Cây Liễu Đầm Lầy Rủ Bóng)**
  ```text
  A miniature stylized low poly weeping willow tree, swollen curvy trunk with exposed buttress roots, graceful tiered cascading foliage clouds hanging down softly in pale spring green, in the distinctive art style of Preserve puzzle game, cozy water landscape prop --ar 1:1
  ```
  *Thông số:* ~450–600 tris | *Pivot:* Đáy gốc cây (Y=0)

* **Variant D (Waterlogged Stump - Gốc Cây Mục Ngập Nước Kèm Nấm)**
  ```text
  A stylized chunky low poly weathered tree stump, cut flat top surface with subtle concentric wood rings, smooth beveled bark with 2 tiny step-like shelf mushrooms and emerald moss patches, in the distinctive art style of Preserve puzzle game, swamp fen scatter prop --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy thân gỗ (Y=0)

* **Variant E (Muddy Hummock - Gò Bùn Nhỏ Kèm Chồi Non)**
  ```text
  A gentle rounded low poly mud hummock island rising above water level, smooth organic shape, bearing two tiny green water reed shoots, cozy clean minimalist water scatter, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~140–180 tris | *Pivot:* Đáy ụ bùn (Y=0)

---

### 2. Lilywater (Hồ Sen Súng & Vườn Nước Thanh Tịnh)
* **Bảng màu:** Xanh ngọc lá sen (Emerald Green), Hồng cánh sen chuyển sắc (Pastel Lotus Pink), Vàng nhị hoa tươi.

* **Variant A (Lilypad Trio with Open Lotus - Cụm 3 Lá Sen Kèm Hoa Nở)**
  ```text
  A stylized low poly floating water lily cluster, three circular emerald-green lilypads with clean notched cuts lying flat, crowned with one oversized blooming lotus blossom in gradient soft pastel pink with a golden-yellow center, in the distinctive art style of Preserve puzzle game, serene pond diorama prop --ar 1:1
  ```
  *Thông số:* ~300–420 tris | *Pivot:* Mặt nước phẳng (Y=0)

* **Variant B (Dual Overlapping Lilypads - Cặp 2 Lá Sen Nổi)**
  ```text
  Two overlapping stylized low poly round lilypads of different sizes resting flat on water, chunky beveled rim, vibrant rich jade green, clean minimalist aquatic plant model, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~140–190 tris | *Pivot:* Mặt nước phẳng (Y=0)

* **Variant C (Emerging Lotus Bud - Búp Sen Vươn Trên Cuống Cong)**
  ```text
  A single stylized low poly lotus flower bud rising gracefully above water on a thick curved green stem, tightly wrapped petals with soft pink tips, accompanied by one small floating pad at the base, in the distinctive art style of Preserve puzzle game, Zen water garden asset --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Mặt nước phẳng (Y=0)

* **Variant D (Water Iris Cluster - Cụm Hoa Diên Vĩ Tím Nước)**
  ```text
  A stylized low poly Japanese water iris plant clump, tall upright sword-like green leaves, three elegant stylized blossoms in royal purple and pastel violet with bright yellow accents, in the distinctive art style of Preserve puzzle game, clean tabletop diorama flower asset --ar 1:1
  ```
  *Thông số:* ~320–420 tris | *Pivot:* Đáy gốc nước (Y=0)

* **Variant E (Duckweed Leaflet Cluster - Mảng Bèo Tấm Nổi Nhẹ)**
  ```text
  A delicate cluster of tiny stylized low poly floating duckweed discs, five small pastel green round pads grouped organically on the water plane, subtle cozy water surface scatter, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~100–150 tris | *Pivot:* Mặt nước phẳng (Y=0)

---

### 3. Streamstone (Suối Đá Rêu & Ghềnh Thác Suối)
* **Bảng màu:** Xám đá cuội mịn (Smooth Slate Gray), Rêu nhung lục bảo (Velvet Emerald Moss), Xanh ngọc nước mát.

* **Variant A (Stepped Mossy Boulders - Ghềnh Đá Xếp Bậc Phủ Rêu)**
  ```text
  A stylized low poly river stone formation, three smooth rounded granite boulders arranged in a natural staircase tier, each topped with a puffy cap of emerald green velvet moss, in the distinctive art style of Preserve puzzle game, cozy river stream diorama asset, clean beveled geometry --ar 1:1
  ```
  *Thông số:* ~280–380 tris | *Pivot:* Đáy khối đá (Y=0)

* **Variant B (Root-Over-Rock - Rễ Cây Cuộn Ôm Đá)**
  ```text
  A stylized low poly smooth ancient tree root arching dramatically over a rounded mossy river pebble into the riverbed, smooth tubular wooden contours, in the distinctive art style of Preserve puzzle game, cozy nature stream prop, clean topology --ar 1:1
  ```
  *Thông số:* ~300–400 tris | *Pivot:* Đáy khối đá (Y=0)

* **Variant C (Trio of River Pebbles - Ba Viên Sỏi Sông Tròn Tròn)**
  ```text
  A cluster of three smooth rounded river pebbles of different sizes nestled together, warm gray slate color with soft beveled contours and a tiny green moss cushion in the crevice, in the distinctive art style of Preserve puzzle game, minimalist scatter prop --ar 1:1
  ```
  *Thông số:* ~120–170 tris | *Pivot:* Đáy cụm sỏi (Y=0)

* **Variant D (Stream-Bank Water Fern - Bụi Dương Xỉ Bờ Suối Xòe Tròn)**
  ```text
  A lush stylized low poly water fern clump, arching broad green fronds fanning gracefully outward from a tiny pebble anchor, vibrant fresh emerald green, in the distinctive art style of Preserve puzzle game, stream-bank vegetation prop --ar 1:1
  ```
  *Thông số:* ~200–280 tris | *Pivot:* Đáy gốc (Y=0)

* **Variant E (Stone Slab Crossing - Cầu Đá Tự Nhiên Bắc Ngang)**
  ```text
  A small rustic stylized low poly stone stepping bridge, two rounded moss-capped boulders supporting a smooth flat stepping stone slab, charming cozy river crossing prop, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy cột đá (Y=0)

---

### 4. Rainleaf (Rừng Mưa Nhiệt Đới & Tán Lá Khổng Lồ)
* **Bảng màu:** Xanh rừng thẳm (Deep Jungle Green), Đỏ cam chuối pháo (Fiery Heliconia Scarlet/Orange), Nâu vỏ cây nhiệt đới.

* **Variant A (Giant Emergent Buttress Tree - Đại Thụ Rễ Bạnh)**
  ```text
  A stylized low poly giant emergent rainforest tree, dramatic flared buttress roots forming wide triangular fins at the base, tall sturdy trunk branching into two broad horizontal layered umbrella foliage pads, rich deep jungle green with generous negative space, in the distinctive art style of Preserve puzzle game, majestic diorama asset --ar 1:1
  ```
  *Thông số:* ~550–700 tris | *Pivot:* Đáy gốc rễ bạnh (Y=0)

* **Variant B (Broadleaf Palm Tree - Cây Lá Rộng Xẻ Thùy / Cọ Lùn)**
  ```text
  A stylized low poly tropical tree with giant oversized monstera and fan palm fronds, thick curvy trunk with smooth surface, bold chunky silhouette, vibrant lime and emerald green gradient, in the distinctive art style of Preserve puzzle game, mobile game ready 3D asset --ar 1:1
  ```
  *Thông số:* ~350–480 tris | *Pivot:* Đáy gốc cây (Y=0)

* **Variant C (Fiery Heliconia Bush - Bụi Chuối Pháo Bông Lớn)**
  ```text
  A cluster of stylized low poly hanging Heliconia flowers, dramatic lobster-claw shaped flower bracts in vibrant fiery scarlet and golden-yellow gradient, framed by oversized chunky tropical banana leaves, in the distinctive art style of Preserve puzzle game, vibrant rainforest centerpiece --ar 1:1
  ```
  *Thông số:* ~320–440 tris | *Pivot:* Đáy bụi cây (Y=0)

* **Variant D (Giant Jungle Fern Clump - Cụm Lá Ráp Khổng Lồ)**
  ```text
  A dense low poly cluster of giant tropical jungle fern fronds sprawling horizontally, thick beveled leaf planes, rich vibrant gradient green, understory rainforest floor prop, in the distinctive art style of Preserve puzzle game --ar 1:1
  ```
  *Thông số:* ~220–300 tris | *Pivot:* Đáy tâm cụm (Y=0)

* **Variant E (Epiphyte Jungle Branch - Nhánh Cây Ký Sinh Rừng Mưa)**
  ```text
  A stylized chunky low poly fallen rainforest branch, overgrown with cute small broadleaf epiphyte orchids and winding vines, deep mossy textures, in the distinctive art style of Preserve puzzle game, ground detail prop --ar 1:1
  ```
  *Thông số:* ~240–320 tris | *Pivot:* Đáy tiếp đất (Y=0)

---

# ⚙️ 3. QUY TRÌNH IMPORT & TỐI ƯU VÀO UNITY (60–120 FPS)

1. **Khi tải file từ AI (Tripo3D, Meshy-4):**
   * Sử dụng công cụ **Retopology / Decimate** trực tiếp trên web của AI:
     * Kéo về **300–500 tris** đối với Cây to.
     * Kéo về **150–250 tris** đối với Bụi cây, Đá, Hoa.
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
