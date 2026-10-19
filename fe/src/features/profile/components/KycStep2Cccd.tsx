import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { kycStep2Schema, type KycStep2Values } from '../schemas/kyc.schema'
import { useKycImageUpload } from '../hooks/useKycSubmit'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { cn } from '@/shared/utils'
import { ChevronLeft, ChevronRight, Camera, Upload, CheckCircle2, AlertCircle } from 'lucide-react'

interface Props {
  defaultValues?: Partial<KycStep2Values>
  onNext: (data: KycStep2Values) => void
  onBack: () => void
}

function ImageUploadZone({
  label,
  preview,
  onFileSelect,
  isUploading,
  hint,
}: {
  label: string
  preview?: string
  onFileSelect: (file: File) => void
  isUploading: boolean
  hint: string
}) {
  const inputRef = useRef<HTMLInputElement>(null)

  const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]
    if (f) onFileSelect(f)
  }

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-foreground">{label}</p>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        className={cn(
          'relative w-full overflow-hidden rounded-xl border-2 border-dashed transition-all',
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
              <div className="rounded-full bg-white/90 p-2">
                <Camera className="size-5 text-zinc-700" />
              </div>
            </div>
            <div className="absolute top-2 right-2">
              <CheckCircle2 className="size-5 text-emerald-400 drop-shadow" />
            </div>
          </>
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-2 py-8 px-4">
            {isUploading ? (
              <div className="size-8 animate-spin rounded-full border-3 border-[#1a8a9d] border-t-transparent" />
            ) : (
              <Upload className="size-6 text-muted-foreground" />
            )}
            <span className="text-xs text-muted-foreground text-center">
              {isUploading ? 'Đang tải ảnh...' : hint}
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

export function KycStep2Cccd({ defaultValues, onNext, onBack }: Props) {
  const [frontPreview, setFrontPreview] = useState<string | undefined>(defaultValues?.cccdFrontPreview)
  const [backPreview, setBackPreview] = useState<string | undefined>(defaultValues?.cccdBackPreview)

  const { mutateAsync: uploadImage, isPending: isUploading } = useKycImageUpload()

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<KycStep2Values>({
    resolver: zodResolver(kycStep2Schema),
    defaultValues: {
      cccdNumber: '',
      ...defaultValues,
    },
  })

  const handleImageUpload = async (file: File, side: 'front' | 'back') => {
    const localUrl = URL.createObjectURL(file)
    if (side === 'front') {
      setFrontPreview(localUrl)
      setValue('cccdFrontPreview', localUrl)
    } else {
      setBackPreview(localUrl)
      setValue('cccdBackPreview', localUrl)
    }

    try {
      await uploadImage({ file, side })
    } catch {
      // Upload failure is non-blocking in mock mode
    }
  }

  const onSubmit = (data: KycStep2Values) => {
    onNext({ ...data, cccdFrontPreview: frontPreview, cccdBackPreview: backPreview })
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {/* CCCD Number */}
      <FormField label="Số CCCD / CMND" error={errors.cccdNumber?.message}>
        <Input
          id="cccd-number"
          placeholder="012345678901"
          inputMode="numeric"
          maxLength={12}
          className="h-11 rounded-xl bg-muted/40 border-border font-mono tracking-widest text-sm focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
          {...register('cccdNumber')}
        />
      </FormField>

      {/* Upload zones */}
      <div className="space-y-4">
        <div className="flex items-start gap-2 rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 p-3">
          <AlertCircle className="size-4 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
          <p className="text-xs text-amber-700 dark:text-amber-300 leading-relaxed">
            Ảnh CCCD phải rõ ràng, không bị che khuất, không bị lóa sáng. Chúng tôi chỉ sử dụng ảnh để xác thực danh tính.
          </p>
        </div>

        <ImageUploadZone
          label="Mặt trước CCCD"
          preview={frontPreview}
          onFileSelect={(f) => handleImageUpload(f, 'front')}
          isUploading={isUploading}
          hint="Chụp hoặc tải ảnh mặt trước CCCD"
        />

        <ImageUploadZone
          label="Mặt sau CCCD"
          preview={backPreview}
          onFileSelect={(f) => handleImageUpload(f, 'back')}
          isUploading={isUploading}
          hint="Chụp hoặc tải ảnh mặt sau CCCD"
        />
      </div>

      {/* Navigation */}
      <div className="flex gap-3 pt-2">
        <Button
          type="button"
          onClick={onBack}
          className="flex-1 h-12 rounded-xl border border-border bg-card text-foreground font-semibold text-sm flex items-center justify-center gap-2 hover:bg-accent active-press"
        >
          <ChevronLeft className="size-4" />
          Quay lại
        </Button>
        <Button
          type="submit"
          className="flex-1 h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm flex items-center justify-center gap-2 active-press"
        >
          Tiếp theo
          <ChevronRight className="size-4" />
        </Button>
      </div>
    </form>
  )
}
