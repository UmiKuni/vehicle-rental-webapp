import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { kycStep2Schema, type KycStep2Values } from '../schemas/kyc.schema'
import { useKycImageUpload } from '../hooks/useKycSubmit'
import { ImageUploadZone } from './ImageUploadZone'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { POPULAR_COUNTRIES } from '@/shared/constants'
import { cn } from '@/shared/utils'
import { ChevronLeft, ChevronRight, AlertCircle, Globe2, CreditCard } from 'lucide-react'

interface Props {
  defaultValues?: Partial<KycStep2Values>
  onNext: (data: KycStep2Values) => void
  onBack: () => void
}

export function KycStep2Cccd({ defaultValues, onNext, onBack }: Props) {
  const [frontPreview, setFrontPreview] = useState<string | undefined>(
    defaultValues?.frontPreview ?? defaultValues?.cccdFrontPreview
  )
  const [backPreview, setBackPreview] = useState<string | undefined>(
    defaultValues?.backPreview ?? defaultValues?.cccdBackPreview
  )

  const { mutateAsync: uploadImage, isPending: isUploading } = useKycImageUpload()

  const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0]

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    control,
    formState: { errors },
  } = useForm<KycStep2Values>({
    resolver: zodResolver(kycStep2Schema),
    defaultValues: {
      docType: defaultValues?.docType ?? 'PASSPORT',
      idNumber: defaultValues?.idNumber ?? defaultValues?.cccdNumber ?? '',
      country: defaultValues?.country ?? 'United States',
      expiryDate: defaultValues?.expiryDate ?? '',
      ...defaultValues,
    },
  })

  const currentDocType = watch('docType')

  const handleImageUpload = async (file: File, side: 'front' | 'back') => {
    const localUrl = URL.createObjectURL(file)
    if (side === 'front') {
      setFrontPreview(localUrl)
      setValue('frontPreview', localUrl)
      setValue('cccdFrontPreview', localUrl)
    } else {
      setBackPreview(localUrl)
      setValue('backPreview', localUrl)
      setValue('cccdBackPreview', localUrl)
    }
    try {
      await uploadImage({ file, side })
    } catch {
      // Non-blocking in mock mode
    }
  }

  const onSubmit = (data: KycStep2Values) => {
    onNext({
      ...data,
      idNumber: data.idNumber.trim().toUpperCase(),
      cccdNumber: data.idNumber.trim().toUpperCase(),
      frontPreview,
      backPreview,
      cccdFrontPreview: frontPreview,
      cccdBackPreview: backPreview,
    })
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      {/* Document Type Selector */}
      <div className="space-y-1.5">
        <label className="text-xs font-semibold text-foreground">Verification Document Type</label>
        <Controller
          name="docType"
          control={control}
          render={({ field }) => (
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => field.onChange('PASSPORT')}
                className={cn(
                  'flex items-center justify-center gap-2 rounded-xl border p-3 text-xs font-bold transition-all min-h-[44px] active-press',
                  field.value === 'PASSPORT'
                    ? 'border-[#1a8a9d] bg-[#1a8a9d]/10 text-[#1a8a9d] ring-1 ring-[#1a8a9d]/30'
                    : 'border-border bg-card text-muted-foreground hover:bg-muted'
                )}
              >
                <Globe2 className="size-4" />
                <span>Passport</span>
              </button>
              <button
                type="button"
                onClick={() => field.onChange('CCCD')}
                className={cn(
                  'flex items-center justify-center gap-2 rounded-xl border p-3 text-xs font-bold transition-all min-h-[44px] active-press',
                  field.value === 'CCCD'
                    ? 'border-[#1a8a9d] bg-[#1a8a9d]/10 text-[#1a8a9d] ring-1 ring-[#1a8a9d]/30'
                    : 'border-border bg-card text-muted-foreground hover:bg-muted'
                )}
              >
                <CreditCard className="size-4" />
                <span>National ID (CCCD)</span>
              </button>
            </div>
          )}
        />
      </div>

      {/* ID / Passport Number */}
      <FormField
        label={currentDocType === 'PASSPORT' ? 'Passport Number' : 'National ID Number'}
        error={errors.idNumber?.message}
      >
        <Input
          id="id-number"
          placeholder={currentDocType === 'PASSPORT' ? 'e.g. B1234567' : '012345678901'}
          maxLength={currentDocType === 'PASSPORT' ? 12 : 12}
          className="h-11 rounded-xl bg-muted/40 border-border font-mono tracking-widest text-sm uppercase focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
          {...register('idNumber')}
        />
      </FormField>

      {/* Passport specific fields */}
      {currentDocType === 'PASSPORT' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label="Nationality / Issuing Country" error={errors.country?.message}>
            <select
              id="passport-country"
              className="h-11 w-full rounded-xl border border-border bg-muted/40 px-3 text-xs font-medium text-foreground focus:border-[#1a8a9d] focus:outline-none focus:ring-1 focus:ring-[#1a8a9d] dark:bg-zinc-800/40"
              {...register('country')}
            >
              {POPULAR_COUNTRIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </FormField>

          <FormField label="Passport Expiry Date" error={errors.expiryDate?.message}>
            <Input
              id="passport-expiry"
              type="date"
              min={tomorrow}
              className="h-11 rounded-xl bg-muted/40 border-border text-xs focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
              {...register('expiryDate')}
            />
          </FormField>
        </div>
      )}

      {/* Upload zones */}
      <div className="space-y-3.5">
        <div className="flex items-start gap-2 rounded-xl bg-teal-500/10 border border-teal-500/20 p-3">
          <AlertCircle className="size-4 text-[#1a8a9d] shrink-0 mt-0.5" />
          <p className="text-[11px] text-muted-foreground leading-relaxed">
            {currentDocType === 'PASSPORT'
              ? 'Please upload a clear photo of your passport information page without glare or cropped edges.'
              : 'National ID photo must be clear on both sides, unobstructed, and valid.'}
          </p>
        </div>

        <ImageUploadZone
          label={currentDocType === 'PASSPORT' ? 'Passport Bio Page' : 'National ID Front'}
          preview={frontPreview}
          onFileSelect={(f) => handleImageUpload(f, 'front')}
          isUploading={isUploading}
          hint={currentDocType === 'PASSPORT' ? 'Take photo or upload passport bio page' : 'Take photo or upload ID front side'}
        />

        <ImageUploadZone
          label={currentDocType === 'PASSPORT' ? 'Visa / Entry Stamp (Optional)' : 'National ID Back'}
          preview={backPreview}
          onFileSelect={(f) => handleImageUpload(f, 'back')}
          isUploading={isUploading}
          hint={currentDocType === 'PASSPORT' ? 'Upload Vietnam visa or entry stamp' : 'Take photo or upload ID back side'}
        />
      </div>

      {/* Navigation Buttons */}
      <div className="flex gap-3 pt-2">
        <Button
          type="button"
          onClick={onBack}
          className="flex-1 h-12 rounded-xl border border-border bg-card text-foreground font-semibold text-sm flex items-center justify-center gap-2 hover:bg-accent active-press"
        >
          <ChevronLeft className="size-4" />
          Back
        </Button>
        <Button
          type="submit"
          className="flex-1 h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm flex items-center justify-center gap-2 active-press"
        >
          Continue
          <ChevronRight className="size-4" />
        </Button>
      </div>
    </form>
  )
}
