import type { UploadRulesDto } from '@/api/generated/models/types/upload-rules-dto'

// Not the rules of a workshop on the Server, so a test proves that the page shows the rules of the answer
class UploadRulesBuilder {
  private readonly rules: UploadRulesDto

  private constructor(rules: UploadRulesDto) {
    this.rules = rules
  }

  static create() {
    return new UploadRulesBuilder({
      fileTypes: [
        { name: 'PNG', mediaType: 'image/png', extensions: ['.png'] },
        { name: 'PDF', mediaType: 'application/pdf', extensions: ['.pdf'] },
      ],
      maxSizeBytes: 2 * 1024 * 1024,
    })
  }

  build(): UploadRulesDto {
    return this.rules
  }
}

export const anUploadRules = () => UploadRulesBuilder.create()
