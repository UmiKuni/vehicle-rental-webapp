import { useRef } from 'react'
import { Camera, Upload, CheckCircle2 } from 'lucide-react'
import { cn } from '@/shared/utils'

interface ImageUploadZoneProps {
  label: string
  preview?: string
  onFileSelect: (file: File) => void
  isUploading: boolean
  hint: string
}

export function ImageUploadZone({
  label,
  preview,
  onFileSelect,
  isUploading,
  hint,
}: ImageUploadZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)

  const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]
    if (f) onFileSelect(f)
  }

  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold text-foreground">{label}</p>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        className={cn(
          'relative w-full overflow-hidden rounded-2xl border-2 border-dashed transition-all active-press min-h-[140px]',
          preview
            ? 'border-emerald-400 bg-emerald-50/50 dark:border-emerald-700 dark:bg-emerald-950/20'
            : 'border-border bg-muted/30 hover:border-[#1a8a9d] hover:bg-[#1a8a9d]/5'
        )}
        style={{ aspectRatio: '16/9' }}
      >
        {preview ? (
          <>
            <img src={preview} alt={label} className="h-full w-full object-cover" />
            <div className="absolute inset-0 flex items-center justify-center bg-black/30 opacity-0 hover:opacity-100 transition-opacity">
              <div className="rounded-full bg-white/90 p-2 shadow">
                <Camera className="size-5 text-zinc-700" />
              </div>
            </div>
            <div className="absolute top-2 right-2">
              <CheckCircle2 className="size-5 text-emerald-500 drop-shadow" />
            </div>
          </>
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-2 py-6 px-4">
            {isUploading ? (
              <div className="size-8 animate-spin rounded-full border-3 border-[#1a8a9d] border-t-transparent" />
            ) : (
              <div className="flex size-11 items-center justify-center rounded-xl bg-muted text-[#1a8a9d]">
                <Upload className="size-5" />
              </div>
            )}
            <span className="text-xs text-muted-foreground text-center font-medium max-w-xs">
              {isUploading ? 'Đang tải ảnh lên...' : hint}
            </span>
          </div>
        )}
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={handleFile}
      />
    </div>
  )
}
