<div align="center">

# ⚔️ DreamRPG (Đang Phát Triển)
**Một Tựa Game Action RPG Hiện Đại Được Xây Dựng Bằng Unity**

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?style=for-the-badge&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-Programming-blue?style=for-the-badge&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Status](https://img.shields.io/badge/Trạng_Thái-Đang_Phát_Triển-orange?style=for-the-badge)](#)

*DreamRPG là một dự án 3D Action RPG đầy tham vọng được thiết kế nhằm phô diễn kỹ năng lập trình gameplay nâng cao, cơ chế chiến đấu theo tiêu chuẩn AAA và kiến trúc phần mềm có khả năng mở rộng tốt trong Unity.*

</div>

---

## 🌟 Về Dự Án

**DreamRPG** hiện đang trong quá trình tích cực phát triển. Trọng tâm cốt lõi của dự án này là xây dựng một nền tảng vững chắc, dễ dàng mở rộng và có tính mô-đun hóa cao cho một tựa game Action RPG hiện đại (tương tự như *Elden Ring* hay *God of War*). Dự án sử dụng triệt để **State Pattern (Mẫu Trạng Thái)** và **Data-Driven Design (Thiết Kế Hướng Dữ Liệu)** để đảm bảo việc thêm mới vũ khí, kẻ thù và kỹ năng luôn diễn ra liền mạch và không phát sinh lỗi.

Dự án này đóng vai trò như một bản giới thiệu kỹ thuật (technical showcase) về khả năng triển khai các hệ thống gameplay phức tạp, tối ưu hóa quy trình xử lý animation và viết mã C# tinh gọn, dễ bảo trì của tôi.

## ✨ Các Tính Năng Cốt Lõi & Điểm Nhấn Kỹ Thuật

### 🛡️ 1. Hệ Thống Chiến Đấu Tiêu Chuẩn AAA
- **Máy Trạng Thái Nâng Cao (Advanced State Machine):** Toàn bộ controller của người chơi được xây dựng dựa trên Máy Trạng Thái phân cấp (`PlayerStateMachine`, `PlayerBaseState`, v.v.), tách biệt hoàn toàn các mô-đun di chuyển, chiến đấu, ngắm bắn và phản ứng khi trúng đòn thành các phần riêng biệt, dễ quản lý.
- **Animation Điều Khiển Bằng Code (Hệ Thống Không-Mũi-Tên):** Loại bỏ hoàn toàn mạng lưới mũi tên chuyển đổi trạng thái chằng chịt trong Animator của Unity. Hệ thống sử dụng `CrossFadeInFixedTime` và Blend Trees được quản lý nghiêm ngặt bằng logic C# để đảm bảo độ phản hồi hoàn hảo đến từng khung hình và không có lỗi chuyển đổi (transition bugs).
- **Di Chuyển Đa Hướng (4-Directional Strafing) Linh Hoạt:** Tích hợp liền mạch 2D Blend Trees với các kiểu di chuyển tương đối theo Camera và tương đối theo Mục tiêu. Bao gồm logic "Xoay tại chỗ" (Turn-In-Place) để ngăn chặn hiện tượng trượt chân (foot-sliding) mà không cần dùng đến root-motion animations.
- **Đỡ Đòn & Phản Đòn Hoàn Hảo (Block & Perfect Parry):** Tích hợp hệ thống quản lý thể lực (stamina), phá thủng phòng ngự (guard breaks), giảm sát thương và khung thời gian chuẩn xác cho perfect parry, đi kèm với các phản hồi trực quan về hình ảnh/âm thanh.

### 🎯 2. Khóa Mục Tiêu Thông Minh & Procedural IK
- **Hệ Thống Hard-Lock:** Một cơ chế khóa camera tinh vi tích hợp mượt mà với Cinemachine. Nó buộc mô hình người chơi phải luôn hướng về phía mục tiêu trong khi vẫn duy trì cơ chế di chuyển đa hướng (strafing) mượt mà.
- **Uốn Cong Cột Sống Bằng Thuật Toán (Procedural Spine Bending - LateUpdate IK):** Mã tùy chỉnh để uốn cong cột sống của người chơi bằng toán học (`SpineBone.rotation`) sao cho khớp với góc nghiêng (pitch) của camera khi ngắm bắn, hoặc khóa khiên hướng về phía trước khi block, ghi đè lên các sai lệch của animation.
- **Đặt Chân Động (Dynamic Foot Placement):** Tích hợp Foot IK tùy chỉnh để đảm bảo bàn chân của nhân vật thích ứng tự nhiên với địa hình gồ ghề và bậc thang.

### ⚔️ 3. Kiến Trúc Kỹ Năng & Vũ Khí Hướng Dữ Liệu
- **Scriptable Objects (`WeaponData` & `SkillData`):** Tất cả vũ khí (Kiếm ngắn, Trọng kiếm, Rìu, Cung) đều hoàn toàn được điều khiển bằng dữ liệu. Việc đổi vũ khí sẽ tự động cập nhật động các chỉ số sát thương, thể lực tiêu hao, tên của trạng thái animation tương ứng và hiệu ứng nguyên tố (VFX) (ví dụ: Rìu Băng vs. Kiếm Sét) mà không cần sửa đổi hệ thống máy trạng thái cốt lõi.
- **Hệ Thống Kỹ Năng Nguyên Tố:** Các trạng thái kỹ năng dạng mô-đun (ví dụ: `PlayerIceAxeSlamState`, `PlayerLightningDomainState`) sử dụng hitbox tùy chỉnh, khởi tạo hệ thống hạt (như khiên năng lượng phong cách Kratos) và áp dụng các hiệu ứng trạng thái (status effect).

---

## 🏗️ Tổng Quan Kiến Trúc

Cấu trúc code được tổ chức chặt chẽ để duy trì tính gắn kết cao (high cohesion) và tính phụ thuộc thấp (low coupling):
```text
📁 Scripts/
├── 📁 Data/           # ScriptableObjects (WeaponData, SkillData, EnemyData)
├── 📁 Player/
│   ├── 📁 StateMachine/ # Các triển khai của Player State Pattern
│   │   ├── PlayerStateMachine.cs (Ngữ cảnh/Context)
│   │   ├── PlayerMoveState.cs
│   │   ├── PlayerBlockState.cs
│   │   └── 📁 Skill/    # Các kỹ năng mô-đun riêng biệt cho từng vũ khí
├── 📁 Enemy/          # AI và Máy Trạng Thái Kẻ Thù dạng mô-đun
├── 📁 Core/           # Quản lý Game, Xử lý Đầu vào, Hệ thống Máu/Thể lực
└── 📁 UI/             # Tâm ngắm động, Thanh máu, Chữ nảy sát thương
```

## 🚀 Trạng Thái Phát Triển Hiện Tại

> **Lưu ý:** : Dự án này hiện đang được tích cực phát triển.[cite: 1] Nhiều tài nguyên hình ảnh (mô hình 3D, animations) hiện chỉ là những nội dung tạm thời (placeholders) nhằm kiểm tra độ bền vững của kiến trúc code bên dưới.[cite: 1]

**Các Cột Mốc Đã Đạt Được Gần Đây:**
- [x] Triển khai hoàn chỉnh Player State Machine.[cite: 1]
- [x] Hệ thống Vũ khí hướng dữ liệu (Cận chiến & Tầm xa).[cite: 1]
- [x] Cơ chế Đỡ đòn bằng khiên, Di chuyển đa hướng (Strafing) và Camera IK chuẩn AAA.[cite: 1]
- [x] Các trạng thái Kỹ năng Nguyên tố ban đầu (Băng, Sét, Gió).[cite: 1]
**Các Công Việc Đang Tiến Hành (WIP):**
- [ ] Tái cấu trúc AI Kẻ thù (Cây Hành Vi / Máy Trạng Thái) để đồng bộ hoàn hảo với các đòn tấn công của người chơi.[cite: 1]
- [ ] Mở rộng hệ thống UI cho Túi đồ (Inventory) và Trang bị (Equipment).[cite: 1]

## 🤝 Contact & Portfolio
I am currently looking for opportunities as a **Unity Developer / Gameplay Programmer**. If you find this architecture interesting, I would love to discuss it further!

## 👨‍💻 Thông tin Tác giả
* **Tác giả:** Lê Tấn Lợi
* **Kỹ năng:** Unity Developer, Software Engineer (Spring Boot, Vue.js, C#).
* **Liên hệ:** loiletan04@gmail.com
