import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { kycStep3Schema, type KycStep3Values } from '../schemas/kyc.schema'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { VN_PROVINCES } from '@/shared/constants'
import { ChevronLeft, Send } from 'lucide-react'

interface Props {
  defaultValues?: Partial<KycStep3Values>
  onSubmit: (data: KycStep3Values) => void
  onBack: () => void
  isSubmitting?: boolean
}

export function KycStep3PersonalInfo({ defaultValues, onSubmit, onBack, isSubmitting }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<KycStep3Values>({
    resolver: zodResolver(kycStep3Schema),
    defaultValues: {
      dateOfBirth: '',
      address: '',
      province: '',
      emergencyContact: '',
      emergencyPhone: '',
      ...defaultValues,
    },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      {/* Personal Info */}
      <div className="space-y-3">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
          Thông tin cá nhân
        </p>

        <FormField label="Ngày sinh" error={errors.dateOfBirth?.message}>
          <Input
            id="date-of-birth"
            type="date"
            max={new Date().toISOString().split('T')[0]}
            className="h-11 rounded-xl bg-muted/40 border-border text-sm focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
            {...register('dateOfBirth')}
          />
        </FormField>

        <FormField label="Địa chỉ thường trú" error={errors.address?.message}>
          <Input
            id="address"
            placeholder="Số nhà, đường, phường/xã, quận/huyện"
            className="h-11 rounded-xl bg-muted/40 border-border text-sm focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
            {...register('address')}
          />
        </FormField>

        <FormField label="Tỉnh / Thành phố" error={errors.province?.message}>
          <select
            id="province"
            className="h-11 w-full rounded-xl border border-border bg-muted/40 px-3 text-sm text-foreground focus:border-[#1a8a9d] focus:outline-none focus:ring-1 focus:ring-[#1a8a9d] dark:bg-zinc-800/40"
            {...register('province')}
          >
            <option value="">Chọn tỉnh/thành</option>
            {VN_PROVINCES.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
          {errors.province && (
            <p className="mt-1 text-xs text-destructive">{errors.province.message}</p>
          )}
        </FormField>
      </div>

      {/* Emergency contact */}
      <div className="space-y-3 pt-1">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
          Liên hệ khẩn cấp
        </p>
        <FormField label="Họ tên người liên hệ" error={errors.emergencyContact?.message}>
          <Input
            id="emergency-contact"
            placeholder="Nguyễn Thị B"
            className="h-11 rounded-xl bg-muted/40 border-border text-sm focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
            {...register('emergencyContact')}
          />
        </FormField>

        <FormField label="Số điện thoại khẩn cấp" error={errors.emergencyPhone?.message}>
          <Input
            id="emergency-phone"
            type="tel"
            placeholder="0901234567"
            inputMode="tel"
            className="h-11 rounded-xl bg-muted/40 border-border text-sm focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d]"
            {...register('emergencyPhone')}
          />
        </FormField>
      </div>

      {/* Submit */}
      <div className="flex gap-3 pt-2">
        <Button
          type="button"
          onClick={onBack}
          className="flex-1 h-12 rounded-xl border border-border bg-card text-foreground font-semibold text-sm flex items-center justify-center gap-2 hover:bg-accent active-press"
        >
          Quay lại
        </Button>
        <Button
          type="submit"
          loading={isSubmitting}
          className="flex-1 h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm flex items-center justify-center gap-2 active-press"
        >
          <Send className="size-4" />
          Gửi hồ sơ
        </Button>
      </div>
    </form>
  )
}
