import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';

class PostItem {
  PostItem.fromJson(Json j)
    : id = j.integer('postId'),
      title = j.str('title'),
      excerpt = j.str('excerpt'),
      visibility = j.str('visibility'),
      authorName = j.str('authorName'),
      createdAt = j.date('createdAt'),
      coverImageId = j.intOrNull('coverImageId'),
      imageCount = j.integer('imageCount');

  final int id;
  final String title;
  final String excerpt;
  final String visibility;
  final String authorName;
  final DateTime createdAt;
  final int? coverImageId;
  final int imageCount;

  String? get coverPath => coverImageId == null ? null : '/api/com/posts/$id/images/$coverImageId';
}

class PostDetail {
  PostDetail.fromJson(Json j)
    : id = j.integer('postId'),
      title = j.str('title'),
      content = j.str('content'),
      visibility = j.str('visibility'),
      authorName = j.str('authorName'),
      createdAt = j.date('createdAt'),
      updatedAt = j.dateOrNull('updatedAt'),
      imageIds = j.list('images', (i) => i.integer('postImageId'));

  final int id;
  final String title;
  final String content;
  final String visibility;
  final String authorName;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final List<int> imageIds;

  String imagePath(int imageId) => '/api/com/posts/$id/images/$imageId';
}

class FeedApi {
  FeedApi(this.ref);
  final Ref ref;

  Future<(List<PostItem>, int)> posts({int page = 1, int pageSize = 10}) async {
    final j = await ref.read(apiProvider).get<Json>('/api/com/posts', query: {'page': page, 'pageSize': pageSize});
    return (j.list('items', PostItem.fromJson), j.integer('total'));
  }

  Future<PostDetail> post(int id) async => PostDetail.fromJson(await ref.read(apiProvider).get<Json>('/api/com/posts/$id'));
}

final feedApiProvider = Provider<FeedApi>(FeedApi.new);

final postDetailProvider = FutureProvider.autoDispose.family<PostDetail, int>((ref, id) => ref.read(feedApiProvider).post(id));
