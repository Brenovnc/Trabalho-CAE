export type CommunityItem = { id: string; title: string; description: string | null; subject: string; activeConceptCount: number; authorName: string; authorInstitution: string | null; sharedAtUtc: string; downloadCount: number; crosswordEnabled: boolean; allowAdvanceReview: boolean; alreadyDownloaded: boolean }
export type CommunityMineItem = Omit<CommunityItem, 'authorName' | 'authorInstitution' | 'alreadyDownloaded'> & { communitySourceModuleId?: string | null }
export type CommunityPageResult = { items: CommunityItem[]; total: number; page: number; pageSize: number }
