import open3d as o3d
import numpy as np

# 加載 Mesh
name = 'cube'
mesh = o3d.io.read_triangle_mesh(name+".ply")

print(mesh)

# 檢查 Mesh 是否有法向量，若沒有則計算法向量
if not mesh.has_vertex_normals():
    mesh.compute_vertex_normals()

# 使用均勻點采樣從 Mesh 表面生成點雲
# 你可以改變 points_num 來控制點雲密度
points_num = 500  # 你可以調整這個值來增加或減少點的數量
point_cloud = mesh.sample_points_poisson_disk(number_of_points=points_num, init_factor=10)

# 計算法向量
# 如果已經有法向量，這個步驟是可選的。這裡保證法向量和點雲同步。
# point_cloud.estimate_normals(search_param=o3d.geometry.KDTreeSearchParamHybrid(radius=0.1, max_nn=30))
# 將點雲數據轉換為 numpy 數組
points = np.asarray(point_cloud.points)

# 計算點之間的最小距離
min_distance = np.inf

# 遍歷每個點並計算其與其他點之間的距離
for i in range(len(points)):
    distances = np.linalg.norm(points[i] - points[i+1:], axis=1)
    if len(distances) > 0:
        min_distance = min(min_distance, np.min(distances))

print(f"點雲中點之間的最小距離: {min_distance}")

# 顯示點雲
o3d.visualization.draw_geometries([point_cloud], point_show_normal=True)

# 將點雲保存到文件中，例如保存為 PLY 格式
o3d.io.write_point_cloud(name+"_pcd.ply", point_cloud,write_ascii=True)
